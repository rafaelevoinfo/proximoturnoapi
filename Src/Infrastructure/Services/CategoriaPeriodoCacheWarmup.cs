using ProximoTurnoApi.Infrastructure.Logging;

namespace ProximoTurnoApi.Infrastructure.Services;

public class CategoriaPeriodoCacheWarmup(
    ICategoriaPeriodoCache cache,
    ILogger<CategoriaPeriodoCacheWarmup> logger) : IHostedService {

    public async Task StartAsync(CancellationToken cancellationToken) {
        using var rastreio = RastreioBackground.Iniciar("AquecimentoCachePeriodos");

        logger.LogInformation("Aquecendo cache de períodos na inicialização.");
        await cache.RefreshAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
