using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases.Backup;

public record ResultadoBackup(bool Sucesso, long TamanhoDumpBytes, int UploadsNovos, string? Erro);

/// <summary>
/// Orquestra uma execução de backup. Não tem dependência de tempo nem de
/// infraestrutura: o agendamento fica no BackgroundService.
/// </summary>
public class ExecutarBackup(
    IDumpBanco dumpBanco,
    IArmazenamentoBackup armazenamento,
    ISincronizadorUploads sincronizadorUploads,
    IEstadoBackupStore estadoStore,
    IEmailService emailService,
    BackupOptions options,
    ILogger<ExecutarBackup> logger)
{
    /// <summary>Abaixo desta fração do dump anterior, consideramos que algo deu errado.</summary>
    private const double FracaoMinimaEmRelacaoAoAnterior = 0.5;

    public async Task<ResultadoBackup> ExecuteAsync(CancellationToken cancellationToken)
    {
        string? caminhoTemporario = null;

        try
        {
            var anterior = await estadoStore.LerAsync();

            logger.LogInformation("Iniciando backup.");

            var dump = await dumpBanco.GerarAsync(cancellationToken);
            caminhoTemporario = dump.CaminhoArquivo;

            VerificarTamanho(dump.TamanhoBytes, anterior);

            var chave = $"db/{DateTime.UtcNow:yyyy-MM-dd}.sql.gz.gpg";
            await armazenamento.EnviarArquivoAsync(chave, dump.CaminhoArquivo, cancellationToken);
            logger.LogInformation("Dump enviado para {Chave} ({Bytes} bytes).", chave, dump.TamanhoBytes);

            // Grava o estado assim que o dump está salvo, antes da sincronização
            // de uploads. O registro significa "um dump de tamanho N foi
            // armazenado no instante T" — isso já é verdade neste ponto. Se a
            // sincronização de uploads falhar depois, o registro do dump (usado
            // na comparação de tamanho e no cálculo da próxima execução) não
            // pode ficar preso a uma execução de noites atrás.
            await estadoStore.GravarAsync(new EstadoBackup(DateTime.UtcNow, dump.TamanhoBytes));

            var sincronizacao = await sincronizadorUploads.SincronizarAsync(cancellationToken);
            logger.LogInformation(
                "{Quantidade} uploads novos sincronizados ({TotalLocal} arquivos locais, {TotalRemoto} chaves no bucket).",
                sincronizacao.NovosEnviados, sincronizacao.TotalArquivosLocais, sincronizacao.TotalChavesRemotas);

            await NotificarSucessoAsync(dump.TamanhoBytes, sincronizacao);

            return new ResultadoBackup(true, dump.TamanhoBytes, sincronizacao.NovosEnviados, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Um redeploy durante a janela de backup cancela o token; isso não
            // é uma falha do backup, e sim um desligamento normal do serviço.
            // Não envia e-mail: "Backup FALHOU" toda vez que alguém publica uma
            // versão nova corroeria a confiança no e-mail como sinal de vida, e
            // SendEmailAsync (sem CancellationToken) bloquearia o StopAsync
            // contra o SIGKILL de 10s do Docker.
            logger.LogInformation("Backup cancelado (desligamento do serviço); nenhum e-mail foi enviado.");
            return new ResultadoBackup(false, 0, 0, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha na execução do backup.");
            await NotificarFalhaAsync(ex.Message);
            return new ResultadoBackup(false, 0, 0, ex.Message);
        }
        finally
        {
            RemoverTemporario(caminhoTemporario);
        }
    }

    private static void VerificarTamanho(long tamanhoAtual, EstadoBackup? anterior)
    {
        // Na primeira execução não há com o que comparar.
        if (anterior is null || anterior.TamanhoDumpBytes <= 0) return;

        var minimo = anterior.TamanhoDumpBytes * FracaoMinimaEmRelacaoAoAnterior;
        if (tamanhoAtual >= minimo) return;

        throw new InvalidOperationException(
            $"Dump de {tamanhoAtual} bytes é muito menor que o anterior, de {anterior.TamanhoDumpBytes} bytes. " +
            "Envio abortado por suspeita de dump incompleto.");
    }

    private async Task NotificarSucessoAsync(long tamanhoBytes, ResultadoSincronizacao sincronizacao)
    {
        var megabytes = tamanhoBytes / 1024d / 1024d;
        var corpo =
            $"<p>Backup concluído em {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC.</p>" +
            $"<ul><li>Dump do banco: {megabytes:F1} MB</li>" +
            $"<li>Uploads novos enviados: {sincronizacao.NovosEnviados}</li>" +
            // Contagens absolutas, não só o delta: um delta de 0 é idêntico
            // numa noite tranquila e num volume de uploads nunca montado. Um
            // "0 locais" repentino aqui é o sinal que denuncia a segunda
            // situação — é exatamente o que o e-mail de sucesso precisa
            // deixar impossível de ignorar.
            $"<li>Arquivos locais encontrados: {sincronizacao.TotalArquivosLocais}</li>" +
            $"<li>Chaves em uploads/ no bucket: {sincronizacao.TotalChavesRemotas}</li></ul>";

        try
        {
            await emailService.SendEmailAsync(
                options.EmailDestino,
                $"Backup OK — {DateTime.UtcNow:yyyy-MM-dd}",
                corpo);
        }
        catch (Exception ex)
        {
            // O backup já foi concluído com sucesso; a falha ao notificar não pode
            // transformar uma execução bem-sucedida em falha.
            logger.LogError(ex, "Não foi possível enviar o e-mail de sucesso do backup.");
        }
    }

    private async Task NotificarFalhaAsync(string erro)
    {
        try
        {
            await emailService.SendEmailAsync(
                options.EmailDestino,
                $"Backup FALHOU — {DateTime.UtcNow:yyyy-MM-dd}",
                $"<p>O backup não foi concluído.</p><pre>{erro}</pre>");
        }
        catch (Exception ex)
        {
            // Falha ao avisar sobre falha não pode derrubar o serviço.
            logger.LogError(ex, "Não foi possível enviar o e-mail de falha do backup.");
        }
    }

    private void RemoverTemporario(string? caminho)
    {
        if (caminho is null || !File.Exists(caminho)) return;

        try
        {
            File.Delete(caminho);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível remover o arquivo temporário {Caminho}.", caminho);
        }
    }
}
