using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases.RAG;

/// <summary>
/// Decide o que fazer com um link de manual e executa. O job só traz ids: o que vale é o
/// estado do banco na hora de rodar, então job repetido ou velho na fila não estraga nada.
/// </summary>
public class SincronizarManual(IWebHostEnvironment _env,
                               ILogger<SincronizarManual> _logger,
                               IIndexacaoManualRepository _repository,
                               IManualVectorStore _vectorStore,
                               ITextExtractor _textExtractor,
                               IRevisorMarkdown _revisor,
                               IChunkingExtractor _chunkingExtractor,
                               IEmbeddingExtractor _embeddingExtractor,
                               IManualQueue _queue) : UseCaseBasico {

    // Falhar tres vezes na mesma URL e sinal de problema no arquivo, nao de instabilidade.
    // Sem esse teto, cada deploy repetiria a cascata inteira de modelos pagos.
    public const int MaxTentativas = 3;

    private const int TamanhoMaximoDoErro = 1000;

    public async Task ExecuteAsync(ManualJob job, CancellationToken cancellationToken) {
        var estado = await _repository.GetEstadoAsync(job.IdJogoLink);

        if (estado is null) {
            await _vectorStore.RemoverAsync(job.IdJogoLink, cancellationToken);
            _logger.LogInformation("Link {IdJogoLink} não existe mais: vetores removidos.", job.IdJogoLink);
            await ReenfileirarDuplicadosAsync(job.IdJogo, job.IdJogoLink);
            return;
        }

        if (estado.Tipo != TipoLink.Regra || string.IsNullOrWhiteSpace(estado.Url) || !estado.JogoAtivo) {
            await RemoverAsync(estado, cancellationToken);
            return;
        }

        var indexacao = estado.Indexacao;
        if (indexacao is not null && indexacao.Url == estado.Url) {
            if (indexacao.Status == StatusIndexacao.Indexado) {
                _logger.LogDebug("Link {IdJogoLink} já indexado nesta URL. Nada a fazer.", estado.IdJogoLink);
                return;
            }

            if (indexacao.Status == StatusIndexacao.Falhou && indexacao.Tentativas >= MaxTentativas) {
                // Manual que nao conseguimos ler nao pode ficar com sobra de vetor antigo
                // respondendo busca em nome dele.
                await _vectorStore.RemoverAsync(estado.IdJogoLink, cancellationToken);
                _logger.LogWarning("Link {IdJogoLink} esgotou as {MaxTentativas} tentativas nesta URL. Só volta se o PDF mudar.",
                                   estado.IdJogoLink, MaxTentativas);
                return;
            }
        }

        await ProcessarAsync(estado, cancellationToken);
    }

    private async Task ProcessarAsync(EstadoLinkManual estado, CancellationToken cancellationToken) {
        var indexacao = estado.Indexacao ?? new JogoLinkIndexacao { IdJogoLink = estado.IdJogoLink, Url = estado.Url };
        var estavaIndexado = indexacao.Status == StatusIndexacao.Indexado && indexacao.Id != 0;

        if (indexacao.Url != estado.Url) {
            indexacao.Url = estado.Url;
            indexacao.Tentativas = 0;
        }

        indexacao.Status = StatusIndexacao.Processando;
        await _repository.SalvarAsync(indexacao);

        // O link fica sem vetores enquanto roda: manual trocado nao pode seguir respondendo
        // com a versao antiga se a nova falhar no meio.
        await _vectorStore.RemoverAsync(estado.IdJogoLink, cancellationToken);

        try {
            await IndexarAsync(estado, indexacao, cancellationToken);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            indexacao.Status = StatusIndexacao.Falhou;
            indexacao.Tentativas++;
            indexacao.UltimoErro = Truncar(ex.Message);
            await _repository.SalvarAsync(indexacao);

            _logger.LogError(ex, "Falha ao indexar o manual do link {IdJogoLink} do jogo {IdJogo} (tentativa {Tentativas}).",
                             estado.IdJogoLink, estado.IdJogo, indexacao.Tentativas);
        }

        if (estavaIndexado) {
            await ReenfileirarDuplicadosAsync(estado.IdJogo, estado.IdJogoLink);
        }
    }

    private async Task IndexarAsync(EstadoLinkManual estado, JogoLinkIndexacao indexacao, CancellationToken cancellationToken) {
        var caminhoPdf = Path.Combine(UploadManual.GetUploadFolder(_env), Path.GetFileName(estado.Url));
        if (!File.Exists(caminhoPdf)) {
            throw new FileNotFoundException($"Arquivo {Path.GetFileName(estado.Url)} não encontrado na pasta de uploads.");
        }

        indexacao.HashPdf = await CalcularHashAsync(caminhoPdf, cancellationToken);

        if (await _repository.ExisteIndexadoComHashAsync(estado.IdJogo, indexacao.HashPdf, estado.IdJogoLink)) {
            indexacao.Status = StatusIndexacao.Duplicado;
            await _repository.SalvarAsync(indexacao);

            _logger.LogInformation("Link {IdJogoLink} aponta para um PDF já indexado no jogo {IdJogo}. Marcado como duplicado.",
                                   estado.IdJogoLink, estado.IdJogo);
            return;
        }

        var contexto = new ContextoManual(estado.NomeJogo, estado.TituloLink);
        var caminhoRaw = await ExtrairAsync(caminhoPdf, indexacao, cancellationToken);
        var caminhoFinal = await RevisarAsync(caminhoRaw, contexto, indexacao, cancellationToken);

        var chunks = await _chunkingExtractor.ExtrairChunksAsync(caminhoFinal, contexto, cancellationToken);
        if (chunks.Count == 0) {
            throw new InvalidOperationException("Nenhum chunk gerado a partir do markdown do manual.");
        }

        var embeddings = await _embeddingExtractor.GerarEmbeddingsAsync(chunks, cancellationToken);
        await _vectorStore.SalvarAsync(estado.IdJogo, estado.IdJogoLink, embeddings, cancellationToken);

        indexacao.Status = StatusIndexacao.Indexado;
        indexacao.Tentativas = 0;
        indexacao.UltimoErro = null;
        indexacao.QuantidadeChunks = chunks.Count;
        indexacao.DataIndexacao = DateTime.Now;
        await _repository.SalvarAsync(indexacao);

        _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo} indexado com {Quantidade} chunks.",
                               estado.IdJogoLink, estado.IdJogo, chunks.Count);
    }

    /// <summary>
    /// Garante o `{hash}.raw.md`. O cache é por conteúdo: o mesmo PDF em dois links, ou
    /// reenviado com outro nome, não é extraído (nem pago) duas vezes.
    /// </summary>
    private async Task<string> ExtrairAsync(string caminhoPdf, JogoLinkIndexacao indexacao, CancellationToken cancellationToken) {
        var pasta = UploadManual.GetUploadFolder(_env);
        var caminhoRaw = Path.Combine(pasta, $"{indexacao.HashPdf}.raw.md");

        if (File.Exists(caminhoRaw)) {
            await CopiarMetadadosAsync(indexacao);
            return caminhoRaw;
        }

        // Formato antigo, nomeado pelo GUID do PDF. Aproveitado uma vez para nao repagar
        // a extracao dos manuais que ja estavam no servidor.
        var legado = Path.ChangeExtension(caminhoPdf, ".md");
        if (File.Exists(legado)) {
            File.Move(legado, caminhoRaw);
            _logger.LogInformation("Markdown legado {Legado} reaproveitado como {Raw}.", Path.GetFileName(legado), Path.GetFileName(caminhoRaw));
            await CopiarMetadadosAsync(indexacao);
            return caminhoRaw;
        }

        var extracao = await _textExtractor.ExtractTextAsync(caminhoPdf, cancellationToken);
        await File.WriteAllTextAsync(caminhoRaw, extracao.Texto, cancellationToken);

        indexacao.ModeloExtracao = extracao.Modelo;
        indexacao.ConfiabilidadeExtracao = (short)extracao.Confiabilidade;
        return caminhoRaw;
    }

    /// <summary>
    /// Garante o `{hash}.md`. Falha total do revisor não impede a indexação: o texto do OCR
    /// ainda vale, e a revisão é tentada de novo na próxima sincronização do link.
    /// </summary>
    private async Task<string> RevisarAsync(string caminhoRaw, ContextoManual contexto, JogoLinkIndexacao indexacao, CancellationToken cancellationToken) {
        var caminhoRevisado = Path.Combine(UploadManual.GetUploadFolder(_env), $"{indexacao.HashPdf}.md");
        if (File.Exists(caminhoRevisado)) {
            return caminhoRevisado;
        }

        var markdown = await File.ReadAllTextAsync(caminhoRaw, cancellationToken);
        var revisao = await _revisor.RevisarAsync(markdown, contexto, cancellationToken);

        indexacao.ModeloRevisao = revisao.Modelo;
        indexacao.CorrecoesAplicadas = revisao.Aplicadas;
        indexacao.CorrecoesDescartadas = revisao.Descartadas;
        indexacao.RevisaoCompleta = revisao.Completa;

        if (revisao.Modelo is null) {
            _logger.LogWarning("Revisão do manual do link {IdJogoLink} falhou inteira. Indexando o texto do OCR.", indexacao.IdJogoLink);
            return caminhoRaw;
        }

        await File.WriteAllTextAsync(caminhoRevisado, revisao.Texto, cancellationToken);
        return caminhoRevisado;
    }

    private async Task RemoverAsync(EstadoLinkManual estado, CancellationToken cancellationToken) {
        await _vectorStore.RemoverAsync(estado.IdJogoLink, cancellationToken);

        var indexacao = estado.Indexacao;
        if (indexacao is null) {
            return;
        }

        var estavaIndexado = indexacao.Status == StatusIndexacao.Indexado;
        indexacao.Status = StatusIndexacao.Removido;
        await _repository.SalvarAsync(indexacao);

        _logger.LogInformation("Link {IdJogoLink} não deve ter vetores (tipo {Tipo}, jogo ativo: {JogoAtivo}). Marcado como removido.",
                               estado.IdJogoLink, estado.Tipo, estado.JogoAtivo);

        if (estavaIndexado) {
            await ReenfileirarDuplicadosAsync(estado.IdJogo, estado.IdJogoLink);
        }
    }

    /// <summary>
    /// Metadados são do conteúdo, não do link: quem reaproveita o cache de um hash herda o
    /// modelo, a nota e o resultado da revisão de quem pagou a extração.
    /// </summary>
    private async Task CopiarMetadadosAsync(JogoLinkIndexacao indexacao) {
        var origem = await _repository.GetMetadadosPorHashAsync(indexacao.HashPdf!);
        if (origem is null || origem.IdJogoLink == indexacao.IdJogoLink) {
            return;
        }

        indexacao.ModeloExtracao = origem.ModeloExtracao;
        indexacao.ConfiabilidadeExtracao = origem.ConfiabilidadeExtracao;
        indexacao.ModeloRevisao = origem.ModeloRevisao;
        indexacao.CorrecoesAplicadas = origem.CorrecoesAplicadas;
        indexacao.CorrecoesDescartadas = origem.CorrecoesDescartadas;
        indexacao.RevisaoCompleta = origem.RevisaoCompleta;
    }

    /// <summary>
    /// Um `Duplicado` só não foi indexado porque outro link do jogo tinha o mesmo PDF.
    /// Quando esse outro sai de cena, os duplicados voltam para a fila e são reavaliados.
    /// </summary>
    private async Task ReenfileirarDuplicadosAsync(int idJogo, int idJogoLinkExceto) {
        if (idJogo == 0) {
            return;
        }

        foreach (var id in await _repository.GetIdsDuplicadosAsync(idJogo, idJogoLinkExceto)) {
            _queue.Enfileirar(new ManualJob(id, idJogo, ""));
        }
    }

    private static async Task<string> CalcularHashAsync(string caminho, CancellationToken cancellationToken) {
        await using var stream = File.OpenRead(caminho);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    private static string Truncar(string mensagem) =>
        mensagem.Length <= TamanhoMaximoDoErro ? mensagem : mensagem[..TamanhoMaximoDoErro];
}
