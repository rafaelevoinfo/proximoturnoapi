using ProximoTurnoApi.Application.UseCases.RAG;
using ProximoTurnoApi.Infrastructure.Logging;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.Workers;

public class IndexacaoManuaisWorker(ILogger<IndexacaoManuaisWorker> _logger,
                                    IManualQueue _queue,
                                    IServiceScopeFactory _scopeFactory) : BackgroundService {

    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        using (RastreioBackground.Iniciar("IndexacaoManuais.Inicializacao")) {
            _logger.LogInformation("IndexacaoManuaisWorker iniciado.");

            var enfileirados = await EnfileirarElegiveisAsync();
            await ReconciliarAsync(enfileirados, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested) {
            // Uma Activity por item da fila: assim as linhas de um manual ficam separadas
            // das do proximo pelo trace id, mesmo saindo intercaladas.
            using var rastreio = RastreioBackground.Iniciar("IndexacaoManual");

            try {
                var job = await _queue.DesenfileirarAsync(stoppingToken);
                _logger.LogInformation("Link {IdJogoLink} retirado da fila de indexação.", job.IdJogoLink);

                // Sequencial de proposito: a extracao chama LLM e nao vale disputar rate limit
                // com ela mesma.
                using var scope = _scopeFactory.CreateScope();
                var sincronizar = scope.ServiceProvider.GetRequiredService<SincronizarManual>();
                await sincronizar.ExecuteAsync(job, stoppingToken);
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
    /// Carga inicial: tudo que ficou pendente enquanto a aplicação estava fora do ar.
    /// Devolve os ids enfileirados para a reconciliação não repetir os mesmos links.
    /// </summary>
    private async Task<HashSet<int>> EnfileirarElegiveisAsync() {
        try {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IIndexacaoManualRepository>();
            var elegiveis = await repository.GetElegiveisAsync(SincronizarManual.MaxTentativas);

            foreach (var job in elegiveis) {
                _queue.Enfileirar(job);
            }

            _logger.LogInformation("{Quantidade} manuais pendentes enfileirados na carga inicial.", elegiveis.Count);
            return [.. elegiveis.Select(job => job.IdJogoLink)];
        } catch (Exception ex) {
            // A carga inicial falhar nao pode impedir o worker de consumir os links novos.
            _logger.LogError(ex, "Falha ao enfileirar os manuais pendentes na carga inicial.");
            return [];
        }
    }

    /// <summary>
    /// Vetores que estão no Qdrant e não deveriam estar: link apagado enquanto a aplicação
    /// estava fora, jogo desativado, link que virou vídeo. Enfileira e deixa o use case decidir.
    /// </summary>
    private async Task ReconciliarAsync(HashSet<int> jaEnfileirados, CancellationToken stoppingToken) {
        try {
            using var scope = _scopeFactory.CreateScope();
            var vectorStore = scope.ServiceProvider.GetRequiredService<IManualVectorStore>();
            var repository = scope.ServiceProvider.GetRequiredService<IIndexacaoManualRepository>();

            var comVetores = await vectorStore.ListarIdsLinksAsync(stoppingToken);
            var esperados = await repository.GetIdsComVetoresEsperadosAsync();

            var orfaos = comVetores.Where(id => !esperados.Contains(id) && !jaEnfileirados.Contains(id)).ToList();
            foreach (var id in orfaos) {
                // IdJogo 0: o link pode nem existir mais, e sem jogo nao ha duplicado a reavaliar.
                _queue.Enfileirar(new ManualJob(id, 0));
            }

            _logger.LogInformation("Reconciliação: {Quantidade} link(s) com vetores sobrando enfileirados.", orfaos.Count);
        } catch (OperationCanceledException) {
            // Desligamento pegou a reconciliacao em andamento: nao e falha, e merge dispara
            // deploy, entao isso aconteceria toda hora e o Error viraria ruido ignorado.
        } catch (Exception ex) {
            _logger.LogError(ex, "Falha ao reconciliar os vetores com o banco.");
        }
    }
}
