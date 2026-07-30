using Microsoft.Extensions.Configuration;
using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class BackupOptionsTests
{
    // Raiz de conteúdo fictícia: os padrões de caminho são derivados dela, então
    // as asserções montam o esperado com Path.Combine em vez de literais com
    // separador fixo, que quebrariam entre Windows e Linux.
    private static readonly string Raiz = Path.Combine(Path.GetTempPath(), "raiz-de-conteudo-teste");

    private static IConfiguration Config(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static BackupOptions Opcoes(Dictionary<string, string?> valores) =>
        BackupOptions.DaConfiguracao(Config(valores), Raiz);

    [Fact]
    public void DaConfiguracao_SemVariaveis_UsaPadroesEmbutidos()
    {
        var options = Opcoes(new());

        Assert.True(options.Habilitado);
        Assert.Equal(new TimeSpan(3, 0, 0), options.Horario);
        Assert.Equal("contato@proximoturno.com.br", options.EmailDestino);
        Assert.Equal("https://s3.us-east-005.backblazeb2.com", options.B2Endpoint);
        Assert.Equal("proximo-turno", options.B2Bucket);
    }

    [Fact]
    public void DaConfiguracao_ComVariaveis_SobrescreveOsPadroes()
    {
        var options = Opcoes(new()
        {
            ["BACKUP_ENABLED"] = "false",
            ["BACKUP_HORA"] = "04:30",
            ["B2_BUCKET"] = "outro-bucket"
        });

        Assert.False(options.Habilitado);
        Assert.Equal(new TimeSpan(4, 30, 0), options.Horario);
        Assert.Equal("outro-bucket", options.B2Bucket);
    }

    [Fact]
    public void DaConfiguracao_HoraInvalida_CaiParaPadrao()
    {
        var options = Opcoes(new() { ["BACKUP_HORA"] = "banana" });

        Assert.Equal(new TimeSpan(3, 0, 0), options.Horario);
    }

    [Fact]
    public void DaConfiguracao_SemCaminhos_DerivaDaRaizDeConteudo()
    {
        // O padrão não pode ser /app fixo: fora do contêiner isso aponta para a
        // raiz do filesystem, onde o processo não tem permissão de escrita.
        var options = Opcoes(new());

        Assert.Equal(Path.Combine(Raiz, "wwwroot", "uploads"), options.CaminhoUploads);
        Assert.Equal(Path.Combine(Raiz, "backup-state", "ultimo-backup.json"), options.CaminhoEstado);
    }

    [Fact]
    public void DaConfiguracao_ComCaminhosExplicitos_IgnoraARaizDeConteudo()
    {
        var options = Opcoes(new()
        {
            ["BACKUP_CAMINHO_UPLOADS"] = "/dados/uploads",
            ["BACKUP_CAMINHO_ESTADO"] = "/dados/estado.json"
        });

        Assert.Equal("/dados/uploads", options.CaminhoUploads);
        Assert.Equal("/dados/estado.json", options.CaminhoEstado);
    }

    [Fact]
    public void CaminhosPadrao_ComRaizDoContainer_PreservamOsCaminhosAnteriores()
    {
        // No contêiner o ContentRootPath é /app (WORKDIR do Dockerfile), então os
        // valores efetivos têm de continuar iguais aos que eram fixos no código —
        // é o que mantém válido o volume api_backup_state:/app/backup-state e o
        // api_uploads:/app/wwwroot/uploads declarados no compose.
        Assert.Equal(
            Path.Combine("/app", "wwwroot", "uploads"),
            BackupOptions.CaminhoUploadsPadrao("/app"));

        Assert.Equal(
            Path.Combine("/app", "backup-state", "ultimo-backup.json"),
            BackupOptions.CaminhoEstadoPadrao("/app"));
    }

    [Fact]
    public void SegredosPresentes_SemSegredos_RetornaFalso()
    {
        var options = Opcoes(new());

        Assert.False(options.SegredosPresentes);
    }

    [Fact]
    public void SegredosPresentes_ComOsTresSegredos_RetornaVerdadeiro()
    {
        var options = Opcoes(new()
        {
            ["BACKUP_PASSPHRASE"] = "senha",
            ["B2_KEY_ID"] = "id",
            ["B2_APPLICATION_KEY"] = "chave"
        });

        Assert.True(options.SegredosPresentes);
    }

    [Fact]
    public void SegredosPresentes_FaltandoUmSegredo_RetornaFalso()
    {
        var options = Opcoes(new()
        {
            ["BACKUP_PASSPHRASE"] = "senha",
            ["B2_KEY_ID"] = "id"
        });

        Assert.False(options.SegredosPresentes);
    }

    [Fact]
    public void RegiaoAutenticacao_EndpointPadrao_ExtraiARegiao()
    {
        var options = new BackupOptions { B2Endpoint = "https://s3.us-east-005.backblazeb2.com" };

        Assert.Equal("us-east-005", options.RegiaoAutenticacao);
    }

    [Fact]
    public void RegiaoAutenticacao_EndpointComBarraFinal_ExtraiARegiao()
    {
        var options = new BackupOptions { B2Endpoint = "https://s3.us-east-005.backblazeb2.com/" };

        Assert.Equal("us-east-005", options.RegiaoAutenticacao);
    }

    [Fact]
    public void RegiaoAutenticacao_EndpointForaDoFormatoEsperado_RetornaNulo()
    {
        // Sem esquema (como o console da Backblaze costuma exibir) não bate com
        // o padrão "https://s3.<regiao>.backblazeb2.com" — nesse caso deixamos
        // sem efeito em vez de arriscar extrair algo errado.
        var options = new BackupOptions { B2Endpoint = "s3.us-east-005.backblazeb2.com" };

        Assert.Null(options.RegiaoAutenticacao);
    }
}
