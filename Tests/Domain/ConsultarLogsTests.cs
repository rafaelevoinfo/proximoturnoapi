using Microsoft.Extensions.Configuration;
using ProximoTurnoApi.Application.UseCases;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ConsultarLogsTests : IDisposable {

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "logs-" + Guid.NewGuid().ToString("N"));
    private readonly ConsultarLogs _useCase;

    public ConsultarLogsTests() {
        Directory.CreateDirectory(_dir);
        _useCase = new ConsultarLogs(new ArquivosLogOptions { Diretorio = _dir, Prefixo = "log", Extensao = ".log" });
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Gravar(string nome, params string[] linhas) =>
        File.WriteAllLines(Path.Combine(_dir, nome), linhas);

    private const string Exemplo1 = "2026-10-03 10:00:00.000 -03:00 [Information] [abc] [PedidosController] Pedido criado";
    private const string Exemplo2 = "2026-10-03 10:00:01.000 -03:00 [Error] [def] [ControllerBasico.EncapsulateRequestAsync] Erro interno no servidor";
    private const string Exemplo3 = "2026-10-03 10:00:02.000 -03:00 [Warning] [] [] Aviso sem trace";

    [Fact]
    public async Task SemArquivos_RetornaVazio() {
        var r = await _useCase.ExecuteAsync(null, null, null, null, null);

        Assert.Null(r.Arquivo);
        Assert.Empty(r.Entradas);
    }

    [Fact]
    public async Task UsaArquivoMaisRecente_EOrdenaDoMaisNovo() {
        Gravar("log20261002.log", Exemplo1);
        Gravar("log20261003.log", Exemplo1, Exemplo2);

        var r = await _useCase.ExecuteAsync(null, null, null, null, null);

        Assert.Equal("log20261003.log", r.Arquivo);
        Assert.Equal(2, r.TotalEncontrado);
        Assert.Equal([2, 1], r.Entradas.Select(e => e.Numero));
        Assert.Equal("Error", r.Entradas[0].Nivel);
        Assert.Equal("def", r.Entradas[0].TraceId);
        Assert.Equal("ControllerBasico.EncapsulateRequestAsync", r.Entradas[0].Origem);
        Assert.Equal("Erro interno no servidor", r.Entradas[0].Mensagem);
    }

    [Fact]
    public async Task LinhasDeContinuacao_ViramDetalhes() {
        Gravar("log20261003.log", Exemplo2, "System.Exception: falhou", "   at X.Y()", Exemplo3);

        var r = await _useCase.ExecuteAsync(null, null, null, null, null);

        var erro = r.Entradas.Single(e => e.Nivel == "Error");
        Assert.Equal("System.Exception: falhou" + Environment.NewLine + "   at X.Y()", erro.Detalhes);
        var aviso = r.Entradas.Single(e => e.Nivel == "Warning");
        Assert.Null(aviso.Detalhes);
        Assert.Null(aviso.TraceId);
        Assert.Null(aviso.Origem);
    }

    [Fact]
    public async Task Filtros_NivelBuscaETrace() {
        Gravar("log20261003.log", Exemplo1, Exemplo2, "System.Exception: TimeoutBanco", Exemplo3);

        Assert.Equal(2, (await _useCase.ExecuteAsync(null, "warning", null, null, null)).TotalEncontrado);
        Assert.Equal(2, (await _useCase.ExecuteAsync(null, null, null, null, null)).Entradas.Count(e => e.Nivel != "Information"));
        // Busca também nos detalhes (exceção) e na origem.
        Assert.Equal("def", (await _useCase.ExecuteAsync(null, null, "timeoutbanco", null, null)).Entradas.Single().TraceId);
        Assert.Equal("abc", (await _useCase.ExecuteAsync(null, null, "pedidoscontroller", null, null)).Entradas.Single().TraceId);
        Assert.Equal(1, (await _useCase.ExecuteAsync(null, null, null, "ABC", null)).TotalEncontrado);
    }

    [Fact]
    public async Task Limite_MantemAsMaisRecentesEContaTodas() {
        Gravar("log20261003.log", Exemplo1, Exemplo2, Exemplo3);

        var r = await _useCase.ExecuteAsync(null, null, null, null, 2);

        Assert.Equal(3, r.TotalEncontrado);
        Assert.Equal([3, 2], r.Entradas.Select(e => e.Numero));
    }

    [Fact]
    public async Task NivelInvalido_Falha() {
        Gravar("log20261003.log", Exemplo1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(null, "Critico", null, null, null));
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\log20261003.log")]
    [InlineData("outro.txt")]
    [InlineData("log20261099.log")]
    public async Task NomeForaDoPadraoOuInexistente_Falha(string nome) {
        Gravar("log20261003.log", Exemplo1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _useCase.ExecuteAsync(nome, null, null, null, null));
    }

    [Fact]
    public void ListarArquivos_IgnoraForaDoPadrao_EOrdenaRolagemPorTamanho() {
        Gravar("log20261003.log", Exemplo1);
        Gravar("log20261003_001.log", Exemplo1);
        Gravar("log20261002.log", Exemplo1);
        Gravar("logantigo.log", Exemplo1);

        Assert.Equal(["log20261003_001.log", "log20261003.log", "log20261002.log"],
            _useCase.ListarArquivos().Select(a => a.Nome));
    }

    [Fact]
    public async Task LeArquivoAbertoParaEscrita() {
        var caminho = Path.Combine(_dir, "log20261003.log");
        using var escritor = new FileStream(caminho, FileMode.Create, FileAccess.Write, FileShare.Read);
        var bytes = System.Text.Encoding.UTF8.GetBytes(Exemplo1 + "\n");
        escritor.Write(bytes);
        escritor.Flush();

        var r = await _useCase.ExecuteAsync(null, null, null, null, null);

        Assert.Single(r.Entradas);
    }

    [Fact]
    public void Opcoes_DerivamDoSinkDeArquivoDoSerilog() {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Serilog:WriteTo:0:Name"] = "Console",
            ["Serilog:WriteTo:1:Name"] = "File",
            ["Serilog:WriteTo:1:Args:path"] = "saida/app.txt",
        }).Build();

        var o = ArquivosLogOptions.DaConfiguracao(config, "/srv/api");

        Assert.Equal(Path.GetFullPath("/srv/api/saida"), o.Diretorio);
        Assert.Equal("app", o.Prefixo);
        Assert.Equal(".txt", o.Extensao);
    }
}
