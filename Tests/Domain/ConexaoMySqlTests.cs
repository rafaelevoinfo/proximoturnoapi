using ProximoTurnoApi.Infrastructure.Backup;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ConexaoMySqlTests
{
    [Fact]
    public void Parse_FormatoUsadoNoDockerCompose_ExtraiTodosOsCampos()
    {
        var conexao = ConexaoMySql.Parse(
            "Server=proximoturno-mysql;Port=3306;Database=proximoturno;User=app;Password=segredo;");

        Assert.Equal("proximoturno-mysql", conexao.Host);
        Assert.Equal(3306, conexao.Porta);
        Assert.Equal("proximoturno", conexao.Banco);
        Assert.Equal("app", conexao.Usuario);
        Assert.Equal("segredo", conexao.Senha);
    }

    [Fact]
    public void Parse_SemPorta_UsaAPadrao3306()
    {
        var conexao = ConexaoMySql.Parse("Server=db;Database=x;User=u;Password=p");

        Assert.Equal(3306, conexao.Porta);
    }

    [Fact]
    public void Parse_UsandoHostEmVezDeServer_Funciona()
    {
        var conexao = ConexaoMySql.Parse("Host=db;Database=x;User=u;Password=p");

        Assert.Equal("db", conexao.Host);
    }

    [Theory]
    [InlineData("Server=db;Database=x;Uid=u;Password=p")]
    [InlineData("Server=db;Database=x;User Id=u;Password=p")]
    public void Parse_VariacoesDoNomeDeUsuario_Funcionam(string connectionString)
    {
        Assert.Equal("u", ConexaoMySql.Parse(connectionString).Usuario);
    }

    [Fact]
    public void Parse_UsandoPwdEmVezDePassword_Funciona()
    {
        Assert.Equal("p", ConexaoMySql.Parse("Server=db;Database=x;User=u;Pwd=p").Senha);
    }

    [Fact]
    public void Parse_ChavesEmCaixaDiferente_Funciona()
    {
        var conexao = ConexaoMySql.Parse("SERVER=db;database=x;uSeR=u;PASSWORD=p");

        Assert.Equal("db", conexao.Host);
        Assert.Equal("x", conexao.Banco);
    }

    [Fact]
    public void Parse_SenhaComSinalDeIgual_PreservaOValorInteiro()
    {
        // Split em 2 partes: senhas base64 terminam em '=' e não podem ser truncadas.
        Assert.Equal("ab=cd==", ConexaoMySql.Parse("Server=db;Database=x;User=u;Password=ab=cd==").Senha);
    }

    [Fact]
    public void Parse_ComEspacosEmVoltaDosValores_RemoveOsEspacos()
    {
        var conexao = ConexaoMySql.Parse("Server = db ; Database = x ; User = u ; Password = p");

        Assert.Equal("db", conexao.Host);
        Assert.Equal("u", conexao.Usuario);
    }

    [Fact]
    public void Parse_PortaNaoNumerica_CaiParaAPadrao()
    {
        Assert.Equal(3306, ConexaoMySql.Parse("Server=db;Port=abc;Database=x;User=u;Password=p").Porta);
    }

    [Theory]
    [InlineData("Database=x;User=u;Password=p", "server")]
    [InlineData("Server=db;User=u;Password=p", "database")]
    [InlineData("Server=db;Database=x;Password=p", "user")]
    [InlineData("Server=db;Database=x;User=u", "password")]
    public void Parse_FaltandoCampoObrigatorio_LancaComNomeDoCampo(string connectionString, string campo)
    {
        var erro = Assert.Throws<InvalidOperationException>(() => ConexaoMySql.Parse(connectionString));

        Assert.Contains(campo, erro.Message, StringComparison.OrdinalIgnoreCase);
    }
}
