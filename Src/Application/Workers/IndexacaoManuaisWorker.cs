using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Application.UseCases;

namespace ProximoTurnoApi.Application.Workers;

public class IndexacaoManuaisWorker(IWebHostEnvironment _env,
                                    ILogger<IndexacaoManuaisWorker> _logger,
                                    IManualQueue _queue,
                                    IServiceScopeFactory _scopeFactory) : BackgroundService {

    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("IndexacaoManuaisWorker iniciado.");

        await EnfileirarPendentesAsync();

        while (!stoppingToken.IsCancellationRequested) {
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

    private async Task ProcessarAsync(ManualJob job, CancellationToken stoppingToken) {
        var nomeArquivo = Path.GetFileName(job.Url);
        var caminhoArquivo = Path.Combine(UploadManual.GetUploadFolder(_env), nomeArquivo);

        if (!File.Exists(caminhoArquivo)) {
            _logger.LogWarning("Arquivo do link {IdJogoLink} do jogo {IdJogo} não encontrado para indexação.", job.IdJogoLink, job.IdJogo);
            return;
        }

        var markdown = Path.ChangeExtension(caminhoArquivo, ".md");
        if (File.Exists(markdown)) {
            _logger.LogDebug("Manual do link {IdJogoLink} já possui markdown extraído. Nada a fazer.", job.IdJogoLink);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var textExtractor = scope.ServiceProvider.GetRequiredService<ITextExtractor>();
        await textExtractor.ExtractTextAsync(caminhoArquivo, stoppingToken);

        _logger.LogInformation("Manual do link {IdJogoLink} do jogo {IdJogo} extraído com sucesso.", job.IdJogoLink, job.IdJogo);
    }
}
