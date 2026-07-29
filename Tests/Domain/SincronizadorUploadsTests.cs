using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.Backup;
using ProximoTurnoApi.Infrastructure.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class SincronizadorUploadsTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"uploads-teste-{Guid.NewGuid()}");

    public SincronizadorUploadsTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private void CriarArquivo(string nome) => File.WriteAllText(Path.Combine(_pasta, nome), "conteudo");

    private class FakeArmazenamento(params string[] chavesExistentes) : IArmazenamentoBackup
    {
        public readonly List<string> ChavesEnviadas = new();

        public Task EnviarArquivoAsync(string chave, string caminhoLocal, CancellationToken cancellationToken)
        {
            ChavesEnviadas.Add(chave);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<string>> ListarChavesAsync(string prefixo, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<string>>(chavesExistentes);
    }

    private SincronizadorUploads Criar(IArmazenamentoBackup armazenamento) =>
        new(armazenamento,
            new BackupOptions { CaminhoUploads = _pasta },
            NullLogger<SincronizadorUploads>.Instance);

    [Fact]
    public async Task SincronizarAsync_BucketVazio_EnviaTodosOsArquivos()
    {
        CriarArquivo("a.pdf");
        CriarArquivo("b.pdf");
        var armazenamento = new FakeArmazenamento();

        var resultado = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(2, resultado.NovosEnviados);
        Assert.Equal(2, resultado.TotalArquivosLocais);
        Assert.Equal(2, resultado.TotalChavesRemotas);
        Assert.Contains("uploads/a.pdf", armazenamento.ChavesEnviadas);
        Assert.Contains("uploads/b.pdf", armazenamento.ChavesEnviadas);
    }

    /// <summary>
    /// Fixa o contrato do consumidor para o caso de prefixo vazio: no primeiro
    /// deploy (ou logo após a política de ciclo de vida do bucket expirar o
    /// prefixo "uploads/") o armazenamento retorna uma coleção vazia, nunca
    /// null. É exatamente esse formato — vazio, mas não nulo — que o
    /// ArmazenamentoB2 real (AWSSDK v4) deve produzir depois do "?? []" em
    /// ListarChavesAsync; este teste documenta que a sincronização trata isso
    /// como situação normal de primeira execução, não como erro.
    /// </summary>
    [Fact]
    public async Task SincronizarAsync_PrefixoVazioComoNaPrimeiraExecucao_SincronizaComSucessoEEnviaTudo()
    {
        CriarArquivo("a.pdf");
        CriarArquivo("b.pdf");
        var armazenamento = new FakeArmazenamento(Array.Empty<string>());

        var resultado = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(2, resultado.NovosEnviados);
        Assert.Contains("uploads/a.pdf", armazenamento.ChavesEnviadas);
        Assert.Contains("uploads/b.pdf", armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task SincronizarAsync_ArquivoJaNoBucket_EnviaApenasOsAusentes()
    {
        CriarArquivo("a.pdf");
        CriarArquivo("b.pdf");
        var armazenamento = new FakeArmazenamento("uploads/a.pdf");

        var resultado = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(1, resultado.NovosEnviados);
        Assert.Equal(2, resultado.TotalArquivosLocais);
        Assert.Equal(2, resultado.TotalChavesRemotas);
        Assert.Equal(new[] { "uploads/b.pdf" }, armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task SincronizarAsync_NadaNovo_NaoEnviaNada()
    {
        CriarArquivo("a.pdf");
        var armazenamento = new FakeArmazenamento("uploads/a.pdf");

        var resultado = await Criar(armazenamento).SincronizarAsync(CancellationToken.None);

        Assert.Equal(0, resultado.NovosEnviados);
        Assert.Equal(1, resultado.TotalArquivosLocais);
        Assert.Equal(1, resultado.TotalChavesRemotas);
        Assert.Empty(armazenamento.ChavesEnviadas);
    }

    [Fact]
    public async Task SincronizarAsync_PastaInexistente_RetornaZeroSemErro()
    {
        var armazenamento = new FakeArmazenamento();
        var sincronizador = new SincronizadorUploads(
            armazenamento,
            new BackupOptions { CaminhoUploads = Path.Combine(_pasta, "nao-existe") },
            NullLogger<SincronizadorUploads>.Instance);

        var resultado = await sincronizador.SincronizarAsync(CancellationToken.None);

        Assert.Equal(0, resultado.NovosEnviados);
        Assert.Equal(0, resultado.TotalArquivosLocais);
        Assert.Equal(0, resultado.TotalChavesRemotas);
    }
}
