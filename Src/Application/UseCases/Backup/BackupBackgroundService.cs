using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Logging;

namespace ProximoTurnoApi.Application.UseCases.Backup;

/// <summary>
/// Dispara o backup uma vez por dia no horário configurado e recupera a
/// execução perdida quando o contêiner reinicia depois da janela.
/// </summary>
public class BackupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEstadoBackupStore estadoStore,
    BackupOptions options,
    ILogger<BackupBackgroundService> logger,
    TimeProvider? timeProvider = null) : BackgroundService {
    private readonly TimeProvider _relogio = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Limiar usado para decidir, dentro do laço, se a janela agendada deve ser
    /// pulada porque um backup já rodou neste mesmo ciclo diário (tipicamente a
    /// execução de recuperação, que pode disparar em qualquer ponto do dia —
    /// minutos ou horas antes da janela agendada, não só na virada da meia-noite).
    /// A regra não é "recente" num sentido vago, é "já rodou neste ciclo": o
    /// valor precisa ficar deliberadamente longe dos dois limites — de 0 (para
    /// cobrir toda variação de horário da recuperação no mesmo dia) e de 24h
    /// (para nunca suprimir uma execução noturna legítima, que cai quase, mas
    /// nunca exatamente, 24h depois da anterior, já que o backup em si consome
    /// alguns minutos). 12h fica no meio do caminho, com folga confortável para
    /// os dois lados.
    /// </summary>
    private static readonly TimeSpan LimiarUltimaExecucaoRecente = TimeSpan.FromHours(12);

    /// <summary>
    /// Verdadeiro quando a última execução bem-sucedida tem mais de 24h — ou
    /// nunca houve uma. Evita que um deploy dentro da janela pule a noite.
    /// </summary>
    public async Task<bool> DeveExecutarAgoraAsync() {
        if (!options.Habilitado) return false;

        var estado = await estadoStore.LerAsync();
        if (estado is null) return true;

        return _relogio.GetUtcNow().UtcDateTime - estado.UltimaExecucaoUtc > TimeSpan.FromHours(24);
    }

    /// <summary>
    /// Verdadeiro quando já houve uma execução bem-sucedida há menos de
    /// <see cref="LimiarUltimaExecucaoRecente"/> — ou seja, já rodou neste
    /// mesmo ciclo diário. Usado pelo laço para não duplicar o backup quando a
    /// execução de recuperação e a janela agendada caem no mesmo dia. Lê o
    /// mesmo estado persistido que <see cref="DeveExecutarAgoraAsync"/> usa —
    /// não há uma segunda fonte de verdade. Público pelo mesmo motivo de
    /// <see cref="DeveExecutarAgoraAsync"/>: permitir testar a decisão com
    /// relógio fixo, sem depender do laço nem de um <see cref="TimeProvider"/>
    /// com temporizador controlável.
    /// </summary>
    public async Task<bool> JaExecutouRecentementeAsync() {
        var estado = await estadoStore.LerAsync();
        if (estado is null) return false;

        return _relogio.GetUtcNow().UtcDateTime - estado.UltimaExecucaoUtc <= LimiarUltimaExecucaoRecente;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using (RastreioBackground.Iniciar("Backup.Inicializacao")) {
            if (!await InicializarAsync(stoppingToken)) return;
        }

        while (!stoppingToken.IsCancellationRequested) {
            // Uma Activity por janela: cada execucao diaria (ou pulo de janela)
            // ganha o seu proprio trace id no log.
            using var rastreio = RastreioBackground.Iniciar("Backup");

            try {
                await Task.Delay(TempoAteProximaExecucao(), _relogio, stoppingToken);

                if (await JaExecutouRecentementeAsync()) {
                    // A execução de recuperação (antes do laço) e a janela agendada
                    // podem cair minutos uma da outra — sem esta checagem, isso
                    // gera um segundo backup completo na mesma madrugada.
                    logger.LogInformation(
                        "Backup já foi executado há menos de {Limiar}; pulando esta janela agendada.",
                        LimiarUltimaExecucaoRecente);
                    continue;
                }

                await ExecutarAsync(stoppingToken);
            } catch (OperationCanceledException) {
                break;
            } catch (Exception ex) {
                // ExecutarBackup.ExecuteAsync captura suas próprias exceções e sempre
                // retorna um ResultadoBackup; então este catch não trata falhas do
                // backup em si, e sim falhas de resolução de DI/escopo (ex.: serviço
                // não registrado), que são o único jeito de chegar aqui.
                logger.LogError(ex, "Erro no laço do serviço de backup.");

                try {
                    await Task.Delay(TimeSpan.FromMinutes(5), _relogio, stoppingToken);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        }

        logger.LogInformation("BackupBackgroundService finalizado.");
    }

    /// <summary>
    /// Checagens de configuração e a execução de recuperação de inicialização.
    /// Retorna <c>false</c> quando o serviço não deve seguir para o laço diário.
    /// </summary>
    private async Task<bool> InicializarAsync(CancellationToken stoppingToken) {
        if (!options.Habilitado) {
            logger.LogInformation("Backup desabilitado por configuração (BACKUP_ENABLED=false).");
            return false;
        }

        if (!options.SegredosPresentes) {
            // Agendar sem os segredos só produziria uma falha por noite e
            // afogaria o e-mail de sucesso, que é o nosso sinal de vida.
            logger.LogError(
                "Backup não será agendado: BACKUP_PASSPHRASE, B2_KEY_ID ou B2_APPLICATION_KEY ausente.");
            return false;
        }

        logger.LogInformation("BackupBackgroundService iniciado. Horário diário: {Horario}.", options.Horario);

        try {
            if (await DeveExecutarAgoraAsync()) {
                logger.LogInformation("Última execução tem mais de 24h. Executando imediatamente.");
                await ExecutarAsync(stoppingToken);
            }
        } catch (OperationCanceledException) {
            return false;
        } catch (Exception ex) {
            // Uma falha aqui (ex.: configuração inválida na construção do
            // IArmazenamentoBackup, ou erro de I/O ao ler o estado) não pode
            // derrubar o host inteiro — BackgroundServiceExceptionBehavior é
            // StopHost por padrão, e o backup mal configurado não pode tirar o
            // sistema de negócio do ar. O agendamento diário abaixo continua
            // valendo mesmo se a recuperação de inicialização falhar.
            logger.LogError(ex, "Falha na execução de recuperação na inicialização do serviço de backup.");
        }

        return true;
    }

    private TimeSpan TempoAteProximaExecucao() {
        // O contêiner roda em America/Sao_Paulo, então o horário local é o esperado.
        var agora = _relogio.GetLocalNow().DateTime;
        var proxima = agora.Date + options.Horario;

        if (proxima <= agora) proxima = proxima.AddDays(1);

        return proxima - agora;
    }

    private async Task ExecutarAsync(CancellationToken stoppingToken) {
        using var scope = scopeFactory.CreateScope();
        var executarBackup = scope.ServiceProvider.GetRequiredService<ExecutarBackup>();

        await executarBackup.ExecuteAsync(stoppingToken);
    }
}
