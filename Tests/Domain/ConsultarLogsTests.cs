using Microsoft.Extensions.Configuration;
using ProximoTurnoApi.Application.DTOs;
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

    private Task<ConsultaLogsDTO> Consultar(FiltroLogs? filtro = null) => _useCase.ExecuteAsync(filtro ?? new FiltroLogs());

    private const string Exemplo1 = "2026-10-03 10:00:00.000 -03:00 [Information] [abc] [PedidosController] Pedido criado";
    private const string Exemplo2 = "2026-10-03 10:00:01.000 -03:00 [Error] [def] [ControllerBasico.EncapsulateRequestAsync] Erro interno no servidor";
    private const string Exemplo3 = "2026-10-03 10:00:02.000 -03:00 [Warning] [] [] Aviso sem trace";

    private static string Linha(string dataHora, string nivel, string mensagem) =>
        $"{dataHora} -03:00 [{nivel}] [t] [Origem] {mensagem}";

    [Fact]
    public async Task SemArquivos_RetornaVazio() {
        var r = await Consultar();

        Assert.Empty(r.Arquivos);
        Assert.Empty(r.Entradas);
    }

    [Fact]
    public async Task UsaArquivoMaisRecente_EOrdenaDoMaisNovo() {
        Gravar("log20261002.log", Exemplo1);
        Gravar("log20261003.log", Exemplo1, Exemplo2);

        var r = await Consultar();

        Assert.Equal(["log20261003.log"], r.Arquivos);
        Assert.Equal(2, r.TotalEncontrado);
        Assert.Equal([2, 1], r.Entradas.Select(e => e.Numero));
        Assert.All(r.Entradas, e => Assert.Equal("log20261003.log", e.Arquivo));
        Assert.Equal("Error", r.Entradas[0].Nivel);
        Assert.Equal("def", r.Entradas[0].TraceId);
        Assert.Equal("ControllerBasico.EncapsulateRequestAsync", r.Entradas[0].Origem);
        Assert.Equal("Erro interno no servidor", r.Entradas[0].Mensagem);
    }

    [Fact]
    public async Task LinhasDeContinuacao_ViramDetalhes() {
        Gravar("log20261003.log", Exemplo2, "System.Exception: falhou", "   at X.Y()", Exemplo3);

        var r = await Consultar();

        var erro = r.Entradas.Single(e => e.Nivel == "Error");
        Assert.Equal("System.Exception: falhou" + Environment.NewLine + "   at X.Y()", erro.Detalhes);
        var aviso = r.Entradas.Single(e => e.Nivel == "Warning");
        Assert.Null(aviso.Detalhes);
        Assert.Null(aviso.TraceId);
        Assert.Null(aviso.Origem);
    }

    [Fact]
    public async Task FiltroNiveis_TrazSoOsEscolhidos() {
        Gravar("log20261003.log", Exemplo1, Exemplo2, Exemplo3);

        var r = await Consultar(new FiltroLogs { Niveis = ["error", "Information"] });

        Assert.Equal(["Error", "Information"], r.Entradas.Select(e => e.Nivel));
    }

    [Fact]
    public async Task NivelInvalido_Falha() {
        Gravar("log20261003.log", Exemplo1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Consultar(new FiltroLogs { Niveis = ["Critico"] }));
    }

    [Fact]
    public async Task Busca_ProcuraEmDetalhesOrigemETrace() {
        Gravar("log20261003.log", Exemplo1, Exemplo2, "System.Exception: TimeoutBanco", Exemplo3);

        Assert.Equal("def", (await Consultar(new FiltroLogs { Busca = "timeoutbanco" })).Entradas.Single().TraceId);
        Assert.Equal("abc", (await Consultar(new FiltroLogs { Busca = "pedidoscontroller" })).Entradas.Single().TraceId);
        Assert.Equal(1, (await Consultar(new FiltroLogs { TraceId = "ABC" })).TotalEncontrado);
    }

    [Fact]
    public async Task Busca_TodasAsPalavrasFrasesEExclusoes() {
        Gravar("log20261003.log",
            Linha("2026-10-03 10:00:00.000", "Information", "Pedido 10 criado pelo cliente"),
            Linha("2026-10-03 10:00:01.000", "Information", "Pedido 11 cancelado pelo cliente"),
            Linha("2026-10-03 10:00:02.000", "Information", "Cliente criado"));

        Assert.Equal(2, (await Consultar(new FiltroLogs { Busca = "pedido cliente" })).TotalEncontrado);
        Assert.Equal(1, (await Consultar(new FiltroLogs { Busca = "pedido -cancelado" })).TotalEncontrado);
        Assert.Equal(1, (await Consultar(new FiltroLogs { Busca = "\"cliente criado\"" })).TotalEncontrado);
        Assert.Equal(2, (await Consultar(new FiltroLogs { Busca = "cliente -\"cliente criado\"" })).TotalEncontrado);
    }

    [Fact]
    public async Task Periodo_CortaPelaHoraEJuntaVariosDias() {
        Gravar("log20261001.log", Linha("2026-10-01 23:00:00.000", "Information", "dia 1"));
        Gravar("log20261002.log",
            Linha("2026-10-02 08:00:00.000", "Information", "dia 2 cedo"),
            Linha("2026-10-02 20:00:00.000", "Information", "dia 2 noite"));
        Gravar("log20261003.log", Linha("2026-10-03 09:00:00.000", "Information", "dia 3"));

        var r = await Consultar(new FiltroLogs {
            Inicio = DateTimeOffset.Parse("2026-10-02T12:00:00-03:00"),
            Fim = DateTimeOffset.Parse("2026-10-03T09:00:00-03:00"),
        });

        // Lê os três dias (folga de fuso), mas só passa o que está no intervalo, fim inclusivo.
        Assert.Equal(["dia 3", "dia 2 noite"], r.Entradas.Select(e => e.Mensagem));
        Assert.Equal(["log20261003.log", "log20261002.log"], r.Entradas.Select(e => e.Arquivo));
        Assert.Contains("log20261002.log", r.Arquivos);
    }

    [Fact]
    public async Task Periodo_ComparaInstanteMesmoEmOutroFuso() {
        Gravar("log20261003.log", Linha("2026-10-03 10:00:00.000", "Information", "dez da manhã em -03"));

        // 13:00 UTC é o mesmo instante que 10:00 -03:00.
        var r = await Consultar(new FiltroLogs { Inicio = DateTimeOffset.Parse("2026-10-03T13:00:00Z") });

        Assert.Single(r.Entradas);
        Assert.Empty((await Consultar(new FiltroLogs { Inicio = DateTimeOffset.Parse("2026-10-03T13:00:01Z") })).Entradas);
    }

    [Fact]
    public async Task Periodo_SoComInicio_LeAteOArquivoMaisNovo() {
        Gravar("log20260920.log", Linha("2026-09-20 10:00:00.000", "Information", "antigo"));
        Gravar("log20261003.log", Linha("2026-10-03 10:00:00.000", "Information", "novo"));
        Gravar("log20261003_001.log", Linha("2026-10-03 18:00:00.000", "Information", "novo parte 2"));

        var r = await Consultar(new FiltroLogs { Inicio = DateTimeOffset.Parse("2026-10-01T00:00:00-03:00") });

        Assert.Equal(["log20261003.log", "log20261003_001.log"], r.Arquivos);
        Assert.Equal(["novo parte 2", "novo"], r.Entradas.Select(e => e.Mensagem));
    }

    [Fact]
    public async Task Periodo_InicioDepoisDoFim_Falha() {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Consultar(new FiltroLogs {
            Inicio = DateTimeOffset.Parse("2026-10-03T10:00:00Z"),
            Fim = DateTimeOffset.Parse("2026-10-02T10:00:00Z"),
        }));
    }

    [Fact]
    public async Task ArquivoExplicito_TemPrecedenciaSobreOPeriodo() {
        Gravar("log20261002.log", Linha("2026-10-02 10:00:00.000", "Information", "dois"));
        Gravar("log20261003.log", Linha("2026-10-03 10:00:00.000", "Information", "três"));

        var r = await Consultar(new FiltroLogs { Arquivo = "log20261002.log" });

        Assert.Equal("dois", r.Entradas.Single().Mensagem);
    }

    [Fact]
    public async Task Limite_MantemAsMaisRecentesEContaTodas() {
        Gravar("log20261003.log", Exemplo1, Exemplo2, Exemplo3);

        var r = await Consultar(new FiltroLogs { Limite = 2 });

        Assert.Equal(3, r.TotalEncontrado);
        Assert.Equal([3, 2], r.Entradas.Select(e => e.Numero));
    }

    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("..\\log20261003.log")]
    [InlineData("outro.txt")]
    [InlineData("log20261099.log")]
    public async Task NomeForaDoPadraoOuInexistente_Falha(string nome) {
        Gravar("log20261003.log", Exemplo1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Consultar(new FiltroLogs { Arquivo = nome }));
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

        var r = await Consultar();

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
