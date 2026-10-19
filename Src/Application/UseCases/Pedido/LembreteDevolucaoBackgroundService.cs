using ProximoTurnoApi.Infrastructure.Logging;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Às 10:00 (horário do contêiner, America/Sao_Paulo) lembra os clientes das devoluções do
/// dia. Se a API subir depois das 10:00, envia na hora o que faltou do dia; o
/// DATA_LEMBRETE_DEVOLUCAO de cada item impede repetir quem já foi avisado.
/// </summary>
public class LembreteDevolucaoBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<LembreteDevolucaoBackgroundService> logger,
    TimeProvider? timeProvider = null) : BackgroundService {

    public static readonly TimeOnly Horario = new(10, 0);

    private readonly TimeProvider _relogio = timeProvider ?? TimeProvider.System;

    public static TimeSpan TempoAteProximaExecucao(DateTime agora) {
        var hoje = agora.Date + Horario.ToTimeSpan();
        var proxima = agora < hoje ? hoje : hoje.AddDays(1);
        return proxima - agora;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var agora = _relogio.GetLocalNow().DateTime;
                if (TimeOnly.FromDateTime(agora) >= Horario) {
                    using var rastreio = RastreioBackground.Iniciar("LembreteDevolucao");
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<EnviarLembretesDevolucao>().ExecuteAsync(agora);
                }
                await Task.Delay(TempoAteProximaExecucao(_relogio.GetLocalNow().DateTime), _relogio, stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                logger.LogError(ex, "Erro no serviço de lembretes de devolução.");
                try {
                    await Task.Delay(TimeSpan.FromMinutes(5), _relogio, stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }
    }
}
