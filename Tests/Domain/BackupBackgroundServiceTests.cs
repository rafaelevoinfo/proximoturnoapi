using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class BackupBackgroundServiceTests
{
    private class FakeEstadoStore(EstadoBackup? inicial) : IEstadoBackupStore
    {
        public Task<EstadoBackup?> LerAsync() => Task.FromResult(inicial);
        public Task GravarAsync(EstadoBackup estado) => Task.CompletedTask;
    }

    private class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    /// <summary>
    /// Registra se um escopo chegou a ser criado, para provar que segredos
    /// ausentes ou o serviço desabilitado impedem qualquer agendamento — a
    /// <see cref="FakeServiceScopeFactory"/> compartilhada não rastreia isso.
    /// </summary>
    private class FakeContadorServiceScopeFactory : IServiceScopeFactory
    {
        public bool EscopoCriado { get; private set; }

        public IServiceScope CreateScope()
        {
            EscopoCriado = true;
            return new FakeServiceScope(new FakeServiceProvider(_ => null));
        }
    }

    /// <summary>
    /// Registra as mensagens logadas para provar que o teste de "desabilitado"
    /// pina especificamente o guard de <c>Habilitado</c> — e não apenas uma
    /// ausência de escopo que outra causa (ex.: cancelamento do laço) também
    /// produziria.
    /// </summary>
    private class FakeLogger<T> : ILogger<T>
    {
        public List<string> Mensagens { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Mensagens.Add(formatter(state, exception));
    }

    /// <summary>
    /// Expõe o <c>ExecuteAsync</c> protegido para chamada direta. Encadear
    /// <c>StartAsync</c>/<c>StopAsync</c> de múltiplas instâncias de
    /// <see cref="BackgroundService"/> no mesmo processo de teste se mostrou
    /// pouco confiável para observar, de forma determinística, efeitos
    /// colaterais síncronos (como mensagens logadas) de uma chamada — o laço
    /// de hospedagem do <see cref="BackgroundService"/> não é o que estes
    /// testes de gating querem exercitar, então chamamos o método diretamente.
    /// </summary>
    private class ServicoExpondoExecuteAsync(
        IServiceScopeFactory scopeFactory,
        IEstadoBackupStore estadoStore,
        BackupOptions options,
        ILogger<BackupBackgroundService> logger,
        TimeProvider? timeProvider)
        : BackupBackgroundService(scopeFactory, estadoStore, options, logger, timeProvider)
    {
        public Task ExecutarDiretamenteAsync(CancellationToken token) => ExecuteAsync(token);
    }

    private static BackupBackgroundService Criar(EstadoBackup? estado, DateTimeOffset agora, bool habilitado = true)
    {
        var scopeFactory = new FakeServiceScopeFactory(new FakeServiceProvider(_ => null));

        return new BackupBackgroundService(
            scopeFactory,
            new FakeEstadoStore(estado),
            new BackupOptions { Habilitado = habilitado },
            NullLogger<BackupBackgroundService>.Instance,
            new RelogioFixo(agora));
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_SemExecucaoAnterior_RetornaVerdadeiro()
    {
        var service = Criar(null, DateTimeOffset.UtcNow);

        Assert.True(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_UltimaExecucaoHaMaisDe24h_RetornaVerdadeiro()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-25), 1000);

        var service = Criar(estado, agora);

        Assert.True(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_UltimaExecucaoRecente_RetornaFalso()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-3), 1000);

        var service = Criar(estado, agora);

        Assert.False(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task DeveExecutarAgoraAsync_Desabilitado_RetornaFalso()
    {
        var service = Criar(null, DateTimeOffset.UtcNow, habilitado: false);

        Assert.False(await service.DeveExecutarAgoraAsync());
    }

    [Fact]
    public async Task JaExecutouRecentementeAsync_SemEstadoAnterior_RetornaFalso()
    {
        var service = Criar(null, DateTimeOffset.UtcNow);

        Assert.False(await service.JaExecutouRecentementeAsync());
    }

    [Fact]
    public async Task JaExecutouRecentementeAsync_UltimaExecucaoAgora_RetornaVerdadeiro()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime, 1000);

        var service = Criar(estado, agora);

        Assert.True(await service.JaExecutouRecentementeAsync());
    }

    [Fact]
    public async Task JaExecutouRecentementeAsync_UltimaExecucaoDentroDoLimiar_RetornaVerdadeiro()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-6), 1000); // 6h < limiar de 12h

        var service = Criar(estado, agora);

        Assert.True(await service.JaExecutouRecentementeAsync());
    }

    [Fact]
    public async Task JaExecutouRecentementeAsync_UltimaExecucaoForaDoLimiar_RetornaFalso()
    {
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-18), 1000); // 18h > limiar de 12h

        var service = Criar(estado, agora);

        Assert.False(await service.JaExecutouRecentementeAsync());
    }

    [Fact]
    public async Task JaExecutouRecentementeAsync_UltimaExecucaoNoLimiarExato_RetornaVerdadeiro()
    {
        // Fixa a escolha de "<=" (inclusive) em vez de "<" no limite exato de 12h.
        var agora = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-12), 1000);

        var service = Criar(estado, agora);

        Assert.True(await service.JaExecutouRecentementeAsync());
    }

    [Fact]
    public async Task ExecuteAsync_SegredosAusentes_NaoCriaEscopoNemAgenda()
    {
        var scopeFactory = new FakeContadorServiceScopeFactory();
        var service = new ServicoExpondoExecuteAsync(
            scopeFactory,
            new FakeEstadoStore(null),
            new BackupOptions { Habilitado = true }, // sem Passphrase/B2KeyId/B2ApplicationKey
            NullLogger<BackupBackgroundService>.Instance,
            new RelogioFixo(DateTimeOffset.UtcNow));

        await service.ExecutarDiretamenteAsync(CancellationToken.None);

        Assert.False(scopeFactory.EscopoCriado);
    }

    [Fact]
    public async Task ExecuteAsync_Desabilitado_NaoCriaEscopoNemAgenda()
    {
        var scopeFactory = new FakeContadorServiceScopeFactory();
        var logger = new FakeLogger<BackupBackgroundService>();
        var service = new ServicoExpondoExecuteAsync(
            scopeFactory,
            new FakeEstadoStore(null),
            new BackupOptions
            {
                // Segredos presentes de propósito: se o guard de Habilitado
                // fosse removido, o guard de segredos não teria como barrar a
                // execução, e o teste passaria mesmo sem a checagem que nomeia.
                Habilitado = false,
                Passphrase = "segredo",
                B2KeyId = "id",
                B2ApplicationKey = "chave"
            },
            logger,
            new RelogioFixo(DateTimeOffset.UtcNow));

        // Token com prazo curto, não CancellationToken.None: se o guard de
        // Habilitado regredir, DeveExecutarAgoraAsync ainda retorna falso (tem
        // sua própria checagem interna de Habilitado), então a recuperação não
        // dispara, mas o laço é alcançado. Dentro do laço, Task.Delay usa
        // relógio de parede real quando o TimeProvider não sobrescreve
        // CreateTimer (é o caso do RelogioFixo, que só fixa GetUtcNow) — com
        // CancellationToken.None esse delay real nunca seria cancelado e o
        // teste travaria de verdade em vez de falhar. Com um prazo curto, o
        // delay é cortado, o laço encerra limpo, e a ausência da mensagem
        // "desabilitado" abaixo derruba o teste rápido e deterministicamente.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await service.ExecutarDiretamenteAsync(cts.Token);

        Assert.False(scopeFactory.EscopoCriado);
        // A ausência de escopo por si só não pinaria o guard de Habilitado
        // especificamente (algum outro motivo também poderia deixar de criar
        // escopo). A mensagem só é logada dentro do "if (!options.Habilitado)"
        // de ExecuteAsync, então sua presença aqui é o que realmente comprova
        // que aquele guard — e não outro — disparou.
        Assert.Contains(logger.Mensagens, m => m.Contains("desabilitado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_TokenJaCancelado_EncerraSemLancarExcecao()
    {
        // Estado recente para que a recuperação não dispare uma execução real
        // antes do laço — o que testamos aqui é o laço encerrando limpo quando
        // o token já chega cancelado, não o caminho de recuperação.
        var agora = DateTimeOffset.UtcNow;
        var estado = new EstadoBackup(agora.UtcDateTime.AddHours(-1), 1000);

        var service = new ServicoExpondoExecuteAsync(
            new FakeServiceScopeFactory(new FakeServiceProvider(_ => null)),
            new FakeEstadoStore(estado),
            new BackupOptions
            {
                Habilitado = true,
                Passphrase = "segredo",
                B2KeyId = "id",
                B2ApplicationKey = "chave"
            },
            NullLogger<BackupBackgroundService>.Instance,
            new RelogioFixo(agora));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var excecao = await Record.ExceptionAsync(() => service.ExecutarDiretamenteAsync(cts.Token));

        Assert.Null(excecao);
    }
}
