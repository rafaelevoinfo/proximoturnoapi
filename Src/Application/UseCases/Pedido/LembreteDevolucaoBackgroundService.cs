namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Às 10:00 lembra os clientes das devoluções do dia. Se a API subir depois das 10:00, envia
/// na hora o que faltou do dia; o DATA_LEMBRETE_DEVOLUCAO de cada item impede repetir quem já
/// foi avisado.
/// </summary>
public class LembreteDevolucaoBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<LembreteDevolucaoBackgroundService> logger,
    TimeProvider? timeProvider = null) : TarefaDiariaBackgroundService(scopeFactory, logger, timeProvider) {

    public static readonly TimeOnly HorarioLembrete = new(10, 0);

    protected override TimeOnly Horario => HorarioLembrete;
    protected override string NomeTarefa => "LembreteDevolucao";

    public static TimeSpan TempoAteProximaExecucao(DateTime agora) => TempoAteProximaExecucao(agora, HorarioLembrete);

    protected override Task ExecutarAsync(IServiceProvider servicos, DateTime agora) =>
        servicos.GetRequiredService<EnviarLembretesDevolucao>().ExecuteAsync(agora);
}
