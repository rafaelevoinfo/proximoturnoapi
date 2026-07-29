using Microsoft.Extensions.Configuration;
using ProximoTurnoApi.Application.UseCases.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class BackupOptionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    [Fact]
    public void DaConfiguracao_SemVariaveis_UsaPadroesEmbutidos()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()));

        Assert.True(options.Habilitado);
        Assert.Equal(new TimeSpan(3, 0, 0), options.Horario);
        Assert.Equal("contato@proximoturno.com.br", options.EmailDestino);
        Assert.Equal("https://s3.us-east-005.backblazeb2.com", options.B2Endpoint);
        Assert.Equal("proximo-turno", options.B2Bucket);
    }

    [Fact]
    public void DaConfiguracao_ComVariaveis_SobrescreveOsPadroes()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()
        {
            ["BACKUP_ENABLED"] = "false",
            ["BACKUP_HORA"] = "04:30",
            ["B2_BUCKET"] = "outro-bucket"
        }));

        Assert.False(options.Habilitado);
        Assert.Equal(new TimeSpan(4, 30, 0), options.Horario);
        Assert.Equal("outro-bucket", options.B2Bucket);
    }

    [Fact]
    public void DaConfiguracao_HoraInvalida_CaiParaPadrao()
    {
        var options = BackupOptions.DaConfiguracao(Config(new() { ["BACKUP_HORA"] = "banana" }));

        Assert.Equal(new TimeSpan(3, 0, 0), options.Horario);
    }

    [Fact]
    public void SegredosPresentes_SemSegredos_RetornaFalso()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()));

        Assert.False(options.SegredosPresentes);
    }

    [Fact]
    public void SegredosPresentes_ComOsTresSegredos_RetornaVerdadeiro()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()
        {
            ["BACKUP_PASSPHRASE"] = "senha",
            ["B2_KEY_ID"] = "id",
            ["B2_APPLICATION_KEY"] = "chave"
        }));

        Assert.True(options.SegredosPresentes);
    }

    [Fact]
    public void SegredosPresentes_FaltandoUmSegredo_RetornaFalso()
    {
        var options = BackupOptions.DaConfiguracao(Config(new()
        {
            ["BACKUP_PASSPHRASE"] = "senha",
            ["B2_KEY_ID"] = "id"
        }));

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
