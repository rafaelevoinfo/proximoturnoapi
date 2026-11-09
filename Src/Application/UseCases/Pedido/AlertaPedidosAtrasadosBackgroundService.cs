namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Às 06:00 manda aos admins o resumo dos pedidos atrasados. Se a API subir depois das 06:00,
/// envia na hora; o DATA_ALERTA_ATRASO de cada item impede repetir o resumo no mesmo dia.
/// </summary>
public class AlertaPedidosAtrasadosBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AlertaPedidosAtrasadosBackgroundService> logger,
    TimeProvider? timeProvider = null) : TarefaDiariaBackgroundService(scopeFactory, logger, timeProvider) {

    public static readonly TimeOnly HorarioAlerta = new(6, 0);

    protected override TimeOnly Horario => HorarioAlerta;
    protected override string NomeTarefa => "AlertaPedidosAtrasados";

    public static TimeSpan TempoAteProximaExecucao(DateTime agora) => TempoAteProximaExecucao(agora, HorarioAlerta);

    protected override Task ExecutarAsync(IServiceProvider servicos, DateTime agora) =>
        servicos.GetRequiredService<EnviarAlertaPedidosAtrasados>().ExecuteAsync(agora);
}
