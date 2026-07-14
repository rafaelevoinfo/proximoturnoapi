namespace ProximoTurnoApi.Infrastructure.Services;

public class CategoriaPeriodoCacheWarmup(
    ICategoriaPeriodoCache cache,
    ILogger<CategoriaPeriodoCacheWarmup> logger) : IHostedService {

    public async Task StartAsync(CancellationToken cancellationToken) {
        logger.LogInformation("Aquecendo cache de períodos na inicialização.");
        await cache.RefreshAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
