using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Application.UseCases.Backup;

namespace ProximoTurnoApi.Infrastructure.Backup;

/// <summary>
/// Envia para o bucket apenas os arquivos ainda ausentes. Os nomes são GUIDs e
/// nunca são sobrescritos, então comparar chaves basta — não é preciso hash.
/// Nada é apagado no bucket: a retenção é da regra de ciclo de vida.
/// </summary>
public class SincronizadorUploads(
    IArmazenamentoBackup armazenamento,
    BackupOptions options,
    ILogger<SincronizadorUploads> logger) : ISincronizadorUploads {
    private const string Prefixo = "uploads/";

    public async Task<ResultadoSincronizacao> SincronizarAsync(CancellationToken cancellationToken) {
        if (!Directory.Exists(options.CaminhoUploads)) {
            logger.LogWarning("Pasta de uploads {Caminho} não existe. Sincronização ignorada.", options.CaminhoUploads);
            return new ResultadoSincronizacao(0, 0, 0);
        }

        var remotas = (await armazenamento.ListarChavesAsync(Prefixo, cancellationToken)).ToHashSet();
        var enviados = 0;
        var totalLocal = 0;

        foreach (var caminhoLocal in Directory.EnumerateFiles(options.CaminhoUploads)) {
            cancellationToken.ThrowIfCancellationRequested();
            totalLocal++;

            var chave = Prefixo + Path.GetFileName(caminhoLocal);
            if (remotas.Contains(chave)) continue;

            await armazenamento.EnviarArquivoAsync(chave, caminhoLocal, cancellationToken);
            enviados++;
        }

        // remotas.Count reflete o que já existia antes desta sincronização;
        // somar os recém-enviados dá o total agora protegido no bucket, sem
        // precisar de uma segunda listagem.
        return new ResultadoSincronizacao(enviados, totalLocal, remotas.Count + enviados);
    }
}
