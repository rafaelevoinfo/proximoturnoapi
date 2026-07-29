using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EstadoBackupArquivoTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"backup-teste-{Guid.NewGuid()}");

    private string Caminho => Path.Combine(_pasta, "ultimo-backup.json");

    public void Dispose()
    {
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public async Task LerAsync_ArquivoInexistente_RetornaNulo()
    {
        var store = new EstadoBackupArquivo(Caminho);

        Assert.Null(await store.LerAsync());
    }

    [Fact]
    public async Task GravarAsync_DepoisLerAsync_DevolveOsMesmosValores()
    {
        var store = new EstadoBackupArquivo(Caminho);
        var momento = new DateTime(2026, 7, 28, 3, 0, 0, DateTimeKind.Utc);

        await store.GravarAsync(new EstadoBackup(momento, 12345));
        var lido = await store.LerAsync();

        Assert.NotNull(lido);
        Assert.Equal(momento, lido!.UltimaExecucaoUtc);
        Assert.Equal(12345, lido.TamanhoDumpBytes);
    }

    [Fact]
    public async Task GravarAsync_CriaODiretorioQuandoNaoExiste()
    {
        var store = new EstadoBackupArquivo(Caminho);

        await store.GravarAsync(new EstadoBackup(DateTime.UtcNow, 1));

        Assert.True(File.Exists(Caminho));
    }

    [Fact]
    public async Task LerAsync_ArquivoCorrompido_RetornaNulo()
    {
        Directory.CreateDirectory(_pasta);
        await File.WriteAllTextAsync(Caminho, "isto nao e json");
        var store = new EstadoBackupArquivo(Caminho);

        Assert.Null(await store.LerAsync());
    }
}
