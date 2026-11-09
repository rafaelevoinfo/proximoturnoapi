using ProximoTurnoApi.Infrastructure.Logging;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Roda uma tarefa uma vez por dia no <see cref="Horario"/> (horário do contêiner,
/// America/Sao_Paulo). Se a API subir depois do horário, roda na hora: cabe à tarefa não
/// repetir o que já fez no dia.
/// </summary>
public abstract class TarefaDiariaBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger logger,
    TimeProvider? timeProvider) : BackgroundService {

    private readonly TimeProvider _relogio = timeProvider ?? TimeProvider.System;

    protected abstract TimeOnly Horario { get; }

    /// <summary>Nome usado no rastreio e nos logs.</summary>
    protected abstract string NomeTarefa { get; }

    protected abstract Task ExecutarAsync(IServiceProvider servicos, DateTime agora);

    public static TimeSpan TempoAteProximaExecucao(DateTime agora, TimeOnly horario) {
        var hoje = agora.Date + horario.ToTimeSpan();
        var proxima = agora < hoje ? hoje : hoje.AddDays(1);
        return proxima - agora;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var agora = _relogio.GetLocalNow().DateTime;
                if (TimeOnly.FromDateTime(agora) >= Horario) {
                    using var rastreio = RastreioBackground.Iniciar(NomeTarefa);
                    using var scope = scopeFactory.CreateScope();
                    await ExecutarAsync(scope.ServiceProvider, agora);
                }
                await Task.Delay(TempoAteProximaExecucao(_relogio.GetLocalNow().DateTime, Horario), _relogio, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                logger.LogError(ex, "Erro na tarefa diária {Tarefa}.", NomeTarefa);
                try {
                    await Task.Delay(TimeSpan.FromMinutes(5), _relogio, stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }
    }
}
