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
        //Realizar chunking do markdown extraído
        //Realizar embedding dos chunks
        //Salvar embeddings no banco vetorial
        //Atualizar o banco indicando que esse jogo ja foi processado
    }

    private async Task<(bool Sucesso, string MarkdownFile)> ExtrairMarkdownAsync(ManualJob job, CancellationToken stoppingToken) {
        return (false, "");
        try {
            var nomeArquivo = Path.GetFileName(job.Url);
            var caminhoArquivo = Path.Combine(UploadManual.GetUploadFolder(_env), nomeArquivo);

            if (!File.Exists(caminhoArquivo)) {
                _logger.LogWarning("Arquivo do link {IdJogoLink} do jogo {IdJogo} não encontrado para indexação.", job.IdJogoLink, job.IdJogo);
                return (false, string.Empty);
            }

            var markdown = Path.ChangeExtension(caminhoArquivo, ".md");
            if (File.Exists(markdown)) {
                _logger.LogDebug("Manual do link {IdJogoLink} já possui markdown extraído. Nada a fazer.", job.IdJogoLink);
                return (false, string.Empty);
            }

            using var scope = _scopeFactory.CreateScope();
            var textExtractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
            var markdownFile = await textExtractor.ExtractTextAsync(caminhoArquivo, stoppingToken);
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