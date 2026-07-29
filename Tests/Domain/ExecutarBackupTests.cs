using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.Backup;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ExecutarBackupTests
{
    private class FakeDumpBanco(long tamanho, Exception? erro = null) : IDumpBanco
    {
        public string CaminhoGerado = Path.Combine(Path.GetTempPath(), $"dump-{Guid.NewGuid()}.sql.gz.gpg");

        public Task<ResultadoDump> GerarAsync(CancellationToken cancellationToken)
        {
            if (erro is not null) throw erro;
            File.WriteAllText(CaminhoGerado, "conteudo");
            return Task.FromResult(new ResultadoDump(CaminhoGerado, tamanho));
        }
    }

    private class FakeArmazenamento : IArmazenamentoBackup
    {
        public readonly List<string> ChavesEnviadas = new();

        public Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken)
        {
            ChavesEnviadas.Add(chave);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
    }

    private class FakeSincronizador(int enviados, int totalLocal = 0, int totalRemoto = 0) : ISincronizadorUploads
    {
        public Task<ResultadoSincronizacao> SincronizarAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ResultadoSincronizacao(enviados, totalLocal, totalRemoto));
    }

    private class FakeSincronizadorQueCancela : ISincronizadorUploads
    {
        public Task<ResultadoSincronizacao> SincronizarAsync(CancellationToken cancellationToken)
            => throw new OperationCanceledException(cancellationToken);
    }

    private class FakeEstadoStore(EstadoBackup? inicial = null) : IEstadoBackupStore
    {
        public EstadoBackup? Estado = inicial;

        public Task<EstadoBackup?> LerAsync() => Task.FromResult(Estado);

        public Task GravarAsync(EstadoBackup estado)
        {
            Estado = estado;
            return Task.CompletedTask;
        }
    }

    private class FakeEmailService : IEmailService
    {
        public readonly List<(string Destino, string Assunto, string Corpo)> Enviados = new();

        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
        {
            Enviados.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    private class FakeEmailServiceQueFalha : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
            => throw new Exception("SMTP indisponível");
    }

    private static ExecutarBackup Criar(
        IDumpBanco dump,
        IArmazenamentoBackup armazenamento,
        ISincronizadorUploads sincronizador,
        IEstadoBackupStore estado,
        IEmailService email) =>
        new(dump, armazenamento, sincronizador, estado, email,
            new BackupOptions { EmailDestino = "destino@teste.com" },
            NullLogger<ExecutarBackup>.Instance);

    [Fact]
    public async Task ExecuteAsync_CaminhoFeliz_EnviaDumpUploadsEEmailDeSucesso()
    {
        var dump = new FakeDumpBanco(1000);
        var armazenamento = new FakeArmazenamento();
        var estado = new FakeEstadoStore();
        var email = new FakeEmailService();

        var resultado = await Criar(dump, armazenamento, new FakeSincronizador(3), estado, email)
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1000, resultado.TamanhoDumpBytes);
        Assert.Equal(3, resultado.UploadsNovos);
        Assert.Single(armazenamento.ChavesEnviadas);
        Assert.StartsWith("db/", armazenamento.ChavesEnviadas[0]);
        Assert.EndsWith(".sql.gz.gpg", armazenamento.ChavesEnviadas[0]);
        Assert.Single(email.Enviados);
        Assert.Contains("OK", email.Enviados[0].Assunto);
        Assert.Equal("destino@teste.com", email.Enviados[0].Destino);
    }

    [Fact]
    public async Task ExecuteAsync_CaminhoFeliz_GravaEstadoComTamanhoDoDump()
    {
        var estado = new FakeEstadoStore();

        await Criar(new FakeDumpBanco(1000), new FakeArmazenamento(), new FakeSincronizador(0), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.NotNull(estado.Estado);
        Assert.Equal(1000, estado.Estado!.TamanhoDumpBytes);
    }

    [Fact]
    public async Task ExecuteAsync_FalhaAoEnviarEmailDeSucesso_AindaAssimReportaSucesso()
    {
        var dump = new FakeDumpBanco(1000);
        var armazenamento = new FakeArmazenamento();

        var resultado = await Criar(dump, armazenamento, new FakeSincronizador(3), new FakeEstadoStore(), new FakeEmailServiceQueFalha())
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1000, resultado.TamanhoDumpBytes);
        Assert.Equal(3, resultado.UploadsNovos);
        Assert.Single(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNoDump_EnviaEmailDeFalhaENaoEnviaArquivo()
    {
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();
        var dump = new FakeDumpBanco(0, new Exception("mysqldump morreu"));

        var resultado = await Criar(dump, armazenamento, new FakeSincronizador(0), new FakeEstadoStore(), email)
            .ExecuteAsync(CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Empty(armazenamento.ChavesEnviadas);
        Assert.Single(email.Enviados);
        Assert.Contains("FALHOU", email.Enviados[0].Assunto);
        Assert.Contains("mysqldump morreu", email.Enviados[0].Corpo);
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNoDump_NaoAtualizaOEstado()
    {
        var anterior = new EstadoBackup(new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc), 999);
        var estado = new FakeEstadoStore(anterior);
        var dump = new FakeDumpBanco(0, new Exception("falhou"));

        await Criar(dump, new FakeArmazenamento(), new FakeSincronizador(0), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.Equal(anterior, estado.Estado);
    }

    [Fact]
    public async Task ExecuteAsync_DumpEncolheuMaisDeMetade_AbortaEAlerta()
    {
        var estado = new FakeEstadoStore(new EstadoBackup(DateTime.UtcNow.AddDays(-1), 1000));
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();

        var resultado = await Criar(new FakeDumpBanco(400), armazenamento, new FakeSincronizador(0), estado, email)
            .ExecuteAsync(CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Empty(armazenamento.ChavesEnviadas);
        Assert.Contains("FALHOU", email.Enviados[0].Assunto);
    }

    [Fact]
    public async Task ExecuteAsync_DumpEncolheuPouco_Prossegue()
    {
        var estado = new FakeEstadoStore(new EstadoBackup(DateTime.UtcNow.AddDays(-1), 1000));
        var armazenamento = new FakeArmazenamento();

        var resultado = await Criar(new FakeDumpBanco(900), armazenamento, new FakeSincronizador(0), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Single(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task ExecuteAsync_PrimeiraExecucao_IgnoraVerificacaoDeTamanho()
    {
        var armazenamento = new FakeArmazenamento();

        var resultado = await Criar(new FakeDumpBanco(1), armazenamento, new FakeSincronizador(0), new FakeEstadoStore(), new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Single(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task ExecuteAsync_SempreRemoveOArquivoTemporario()
    {
        var dump = new FakeDumpBanco(1000);

        await Criar(dump, new FakeArmazenamento(), new FakeSincronizador(0), new FakeEstadoStore(), new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.False(File.Exists(dump.CaminhoGerado));
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNaSincronizacaoDeUploads_ReportaFalhaMasODumpJaSubiu()
    {
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();

        var resultado = await Criar(new FakeDumpBanco(1000), armazenamento, new SincronizadorQueFalha(), new FakeEstadoStore(), email)
            .ExecuteAsync(CancellationToken.None);

        // O dump já subiu; a falha dos uploads é reportada mas não descarta o backup do banco.
        Assert.False(resultado.Sucesso);
        Assert.Single(armazenamento.ChavesEnviadas);
        Assert.Contains("FALHOU", email.Enviados[0].Assunto);
    }

    private class SincronizadorQueFalha : ISincronizadorUploads
    {
        public Task<ResultadoSincronizacao> SincronizarAsync(CancellationToken cancellationToken)
            => throw new Exception("bucket indisponivel");
    }

    [Fact]
    public async Task ExecuteAsync_FalhaNaSincronizacaoDeUploads_AindaAssimGravaOEstadoDoDump()
    {
        // O dump já foi salvo com sucesso quando a sincronização de uploads
        // falha; o registro de estado (usado na comparação de tamanho e no
        // agendamento) não pode ficar preso a uma execução anterior só porque
        // uma etapa posterior e independente falhou.
        var estado = new FakeEstadoStore();

        await Criar(new FakeDumpBanco(1000), new FakeArmazenamento(), new SincronizadorQueFalha(), estado, new FakeEmailService())
            .ExecuteAsync(CancellationToken.None);

        Assert.NotNull(estado.Estado);
        Assert.Equal(1000, estado.Estado!.TamanhoDumpBytes);
    }

    [Fact]
    public async Task ExecuteAsync_CaminhoFeliz_EmailReportaTotaisAbsolutosDeUploads()
    {
        var armazenamento = new FakeArmazenamento();
        var email = new FakeEmailService();

        await Criar(new FakeDumpBanco(1000), armazenamento, new FakeSincronizador(2, totalLocal: 5, totalRemoto: 20), new FakeEstadoStore(), email)
            .ExecuteAsync(CancellationToken.None);

        var corpo = email.Enviados[0].Corpo;
        Assert.Contains("Arquivos locais encontrados: 5", corpo);
        Assert.Contains("Chaves em uploads/ no bucket: 20", corpo);
    }

    [Fact]
    public async Task ExecuteAsync_TokenCanceladoDuranteSincronizacao_NaoEnviaEmailDeFalha()
    {
        var email = new FakeEmailService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var resultado = await Criar(new FakeDumpBanco(1000), new FakeArmazenamento(), new FakeSincronizadorQueCancela(), new FakeEstadoStore(), email)
            .ExecuteAsync(cts.Token);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Erro);
        Assert.Empty(email.Enviados);
    }
}
