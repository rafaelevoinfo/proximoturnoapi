using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Logging;

namespace ProximoTurnoApi.Application.Workers;

public class IndexacaoManuaisWorker(IWebHostEnvironment _env,
                                    ILogger<IndexacaoManuaisWorker> _logger,
                                    IManualQueue _queue,
                                    IServiceScopeFactory _scopeFactory) : BackgroundService {

    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        using (RastreioBackground.Iniciar("IndexacaoManuais.Inicializacao")) {
            _logger.LogInformation("IndexacaoManuaisWorker iniciado.");

            await EnfileirarPendentesAsync();
        }

        while (!stoppingToken.IsCancellationRequested) {
            // Uma Activity por item da fila: assim as linhas de um manual ficam
            // separadas das do proximo pelo trace id, mesmo saindo intercaladas.
            using var rastreio = RastreioBackground.Iniciar("IndexacaoManual");

            try {
                var job = await _queue.DesenfileirarAsync(stoppingToken);
                _logger.LogInformation("Manual do link {IdJogoLink} retirado da fila de indexação.", job.IdJogoLink);

                // Sequencial de proposito: extracao chama LLM e nao vale disputar rate limit com ela mesma.
                await ProcessarAsync(job, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                _logger.LogError(ex, "Erro no loop de indexação de manuais.");

                // Evita busy loop caso a falha seja no proprio desenfileiramento.
                try {
                    await Task.Delay(1000, stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }

        _logger.LogInformation("IndexacaoManuaisWorker finalizado.");
    }



    private async Task ProcessarAsync(ManualJob job, CancellationToken stoppingToken) {
        var (sucesso, markdownFile) = await ExtrairMarkdownAsync(job, stoppingToken);
        if (!sucesso) {
            _logger.LogWarning("Falha ao extrair markdown do manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return;
        }

        var chunks = await ChunkingAsync(job, markdownFile, stoppingToken);
        if (chunks.Count == 0) {
            _logger.LogWarning("Nenhum chunk gerado para o manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return;
        }

        var embeddings = await EmbeddingAsync(job, chunks, stoppingToken);
        if (embeddings.Count == 0) {
            _logger.LogWarning("Nenhum embedding gerado para o manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return;
        }

        if (!await SalvarVetoresAsync(job, embeddings, stoppingToken)) {
            return;
        }

        await MarcarIndexadoAsync(job);
    }

    /// <summary>
    /// Grava os vetores no Qdrant. Só depois disso o link pode ser marcado como indexado:
    /// marcar antes deixaria o manual fora da fila sem nunca ter sido gravado.
    /// </summary>
    private async Task<bool> SalvarVetoresAsync(ManualJob job, IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken stoppingToken) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IManualVectorStore>();
            await vectorStore.SalvarAsync(job.IdJogo, job.IdJogoLink, embeddings, stoppingToken);
            return true;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro ao gravar os vetores do manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return false;
        }
    }

    /// <summary>
    /// Fecha o ciclo do manual. Falhar aqui só custa reprocessar o manual no próximo
    /// start: o Qdrant apaga os vetores antigos do link antes de gravar de novo.
    /// </summary>
    private async Task MarcarIndexadoAsync(ManualJob job) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var jogoRepository = scope.ServiceProvider.GetRequiredService<IJogoRepository>();
            await jogoRepository.MarcarIndexadoAsync(job.IdJogoLink);

            _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo} indexado com sucesso.", job.IdJogoLink, job.IdJogo);
        } catch (Exception ex) {
            _logger.LogError(ex, "Vetores gravados, mas falhou ao marcar o link {IdJogoLink} como indexado.", job.IdJogoLink);
        }
    }

    /// <summary>
    /// Gera os vetores dos chunks. Como a chamada e paga, uma falha aqui para este manual
    /// e nao e retentada: o link continua nao indexado e volta na proxima carga inicial.
    /// </summary>
    private async Task<IReadOnlyList<ChunkEmbedding>> EmbeddingAsync(ManualJob job, IReadOnlyList<ManualChunk> chunks, CancellationToken stoppingToken) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var embeddingExtractor = scope.ServiceProvider.GetRequiredService<IEmbeddingExtractor>();
            var embeddings = await embeddingExtractor.GerarEmbeddingsAsync(chunks, stoppingToken);

            _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo}: {Quantidade} embeddings gerados.",
                                   job.IdJogoLink, job.IdJogo, embeddings.Count);
            return embeddings;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro ao gerar embeddings do manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return [];
        }
    }

    /// <summary>
    /// Quebra o markdown em chunks. Falhar aqui derruba so este manual: o proximo da fila
    /// nao tem nada a ver com um markdown malformado.
    /// </summary>
    private async Task<IReadOnlyList<ManualChunk>> ChunkingAsync(ManualJob job, string markdownFile, CancellationToken stoppingToken) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var chunkingExtractor = scope.ServiceProvider.GetRequiredService<IChunkingExtractor>();
            var chunks = await chunkingExtractor.ExtrairChunksAsync(markdownFile, stoppingToken);

            _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo} dividido em {Quantidade} chunks.",
                                   job.IdJogoLink, job.IdJogo, chunks.Count);
            return chunks;
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro ao dividir em chunks o manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return [];
        }
    }

    private async Task<(bool Sucesso, string MarkdownFile)> ExtrairMarkdownAsync(ManualJob job, CancellationToken stoppingToken) {
        try {
            var nomeArquivo = Path.GetFileName(job.Url);
            var caminhoArquivo = Path.Combine(UploadManual.GetUploadFolder(_env), nomeArquivo);

            if (!File.Exists(caminhoArquivo)) {
                _logger.LogWarning("Arquivo do link {IdJogoLink} do jogo {IdJogo} não encontrado para indexação.", job.IdJogoLink, job.IdJogo);
                return (false, string.Empty);
            }

            var markdownFile = Path.ChangeExtension(caminhoArquivo, ".md");
            if (File.Exists(markdownFile)) {
                _logger.LogDebug("Manual do link {IdJogoLink} já possui markdown extraído. Nada a fazer.", job.IdJogoLink);
                return (true, markdownFile);
            }

            using var scope = _scopeFactory.CreateScope();
            var textExtractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
            markdownFile = await textExtractor.ExtractTextAsync(caminhoArquivo, stoppingToken);
            _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo} extraído com sucesso.", job.IdJogoLink, job.IdJogo);
            return (true, markdownFile);
        } catch (Exception ex) {
            _logger.LogError(ex, "Erro ao extrair markdown do manual do link {IdJogoLink} do jogo {IdJogo}.", job.IdJogoLink, job.IdJogo);
            return (false, string.Empty);
        }
    }

    /// <summary>
    /// Carga inicial da fila: tudo que ficou pendente enquanto a aplicação estava fora do ar.
    /// </summary>
    private async Task EnfileirarPendentesAsync() {
        try {
            using var scope = _scopeFactory.CreateScope();
            var jogoRepository = scope.ServiceProvider.GetRequiredService<IJogoRepository>();
            var pendentes = await jogoRepository.GetJogosNaoIndexadosAsync();

            foreach (var link in pendentes) {
                _queue.Enfileirar(new ManualJob(link.Id, link.IdJogo, link.Url));
            }

            _logger.LogInformation("{Quantidade} manuais pendentes enfileirados na carga inicial.", pendentes.Count);
        } catch (Exception ex) {
            // A carga inicial falhar nao pode impedir o worker de consumir os links novos.
            _logger.LogError(ex, "Falha ao enfileirar os manuais pendentes na carga inicial.");
        }
    }
}