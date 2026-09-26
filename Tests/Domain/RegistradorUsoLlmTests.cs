using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.IA;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class RegistradorUsoLlmTests {

    private readonly FakeUsoLlmRepository _repositorio = new();
    private readonly FakeCotacaoDolar _cotacao = new(5m);

    private RegistradorUsoLlm Registrador() {
        var servicos = new ServiceCollection();
        servicos.AddScoped<IUsoLlmRepository>(_ => _repositorio);

        return new RegistradorUsoLlm(NullLogger<RegistradorUsoLlm>.Instance,
                                     servicos.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                                     _cotacao);
    }

    private static RegistroUsoLlm Registro(string? detalhe = null) =>
        new(OperacaoLlm.RevisaoMarkdown, "deepseek/deepseek-v4-flash", "deepseek/deepseek-v4-flash-20260423",
            "Parasail", 1533, 409, 11, 7, 0.0000068600m, 8077, DesfechoLlm.Ok, detalhe, "gen-teste-1");

    [Fact]
    public async Task Registro_ViraLinhaComTodosOsCampos() {
        await Registrador().RegistrarAsync(Registro());

        var linha = Assert.Single(_repositorio.Gravados);
        Assert.Equal(OperacaoLlm.RevisaoMarkdown, linha.Operacao);
        Assert.Equal("deepseek/deepseek-v4-flash", linha.ModeloPedido);
        Assert.Equal("deepseek/deepseek-v4-flash-20260423", linha.ModeloRespondeu);
        Assert.Equal("Parasail", linha.Provider);
        Assert.Equal(1533, linha.TokensEntrada);
        Assert.Equal(409, linha.TokensSaida);
        Assert.Equal(11, linha.TokensRaciocinio);
        Assert.Equal(7, linha.TokensCache);
        Assert.Equal(0.0000068600m, linha.CustoUsd);
        Assert.Equal(8077, linha.DuracaoMs);
        Assert.Equal(DesfechoLlm.Ok, linha.Desfecho);
        Assert.Equal("gen-teste-1", linha.IdGeracao);
        Assert.NotEqual(default, linha.Momento);
    }

    [Fact]
    public async Task ComEscopoAberto_LinhaGanhaOAlvo() {
        using (EscopoUsoLlm.Abrir(17, 14, "Balde De Caranguejo / FAQ")) {
            await Registrador().RegistrarAsync(Registro());
        }

        var linha = Assert.Single(_repositorio.Gravados);
        Assert.Equal(17, linha.IdJogo);
        Assert.Equal(14, linha.IdJogoLink);
        Assert.Equal("Balde De Caranguejo / FAQ", linha.Alvo);
    }

    [Fact]
    public async Task SemEscopo_GravaODinheiroComAlvoEmBranco() {
        await Registrador().RegistrarAsync(Registro());

        var linha = Assert.Single(_repositorio.Gravados);
        Assert.Null(linha.IdJogo);
        Assert.Null(linha.IdJogoLink);
        Assert.Null(linha.Alvo);
    }

    // Trava do item 5 do Review Focus: corpo de erro HTTP nao cabe em DETALHE(300), e no
    // MySQL estrito isso derrubaria justamente a linha do erro.
    [Fact]
    public async Task TextoMaiorQueAColuna_EhCortado() {
        var detalheGigante = new string('x', 900);

        using (EscopoUsoLlm.Abrir(17, 14, new string('j', 500))) {
            await Registrador().RegistrarAsync(Registro(detalheGigante));
        }

        var linha = Assert.Single(_repositorio.Gravados);
        Assert.Equal(300, linha.Detalhe?.Length);
        Assert.Equal(200, linha.Alvo?.Length);
    }

    [Fact]
    public async Task RepositorioQueLanca_NaoPropaga() {
        _repositorio.Erro = new InvalidOperationException("banco fora");

        await Registrador().RegistrarAsync(Registro());

        Assert.Empty(_repositorio.Gravados);
    }

    [Fact]
    public void CaminhoSincrono_GravaIgual() {
        Registrador().Registrar(Registro());

        Assert.Single(_repositorio.Gravados);
    }

    [Fact]
    public void CaminhoSincrono_RepositorioQueLanca_NaoPropaga() {
        _repositorio.Erro = new InvalidOperationException("banco fora");

        Registrador().Registrar(Registro());

        Assert.Empty(_repositorio.Gravados);
    }

    [Fact]
    public async Task SemUsuarioNoEscopo_NaoConverteParaReais() {
        using (EscopoUsoLlm.Abrir(17, 14, "manual")) {
            await Registrador().RegistrarAsync(Registro());
        }

        var linha = Assert.Single(_repositorio.Gravados);
        Assert.Null(linha.IdUsuario);
        Assert.Null(linha.CustoBrl);
        Assert.Null(linha.CotacaoUsdBrl);
    }

    [Fact]
    public async Task ComUsuarioNoEscopo_GravaUsuarioECustoEmReais() {
        _cotacao.Valor = 5.5m;

        using (EscopoUsoLlm.Abrir(17, null, "Chat de regras / Catan", "usuario-1")) {
            await Registrador().RegistrarAsync(Registro());
        }

        var linha = Assert.Single(_repositorio.Gravados);
        Assert.Equal("usuario-1", linha.IdUsuario);
        Assert.Equal(0.0000068600m * 5.5m, linha.CustoBrl);
        Assert.Equal(5.5m, linha.CotacaoUsdBrl);
    }

    // Custo nulo nao pode sair de graca do credito: entra a estimativa pelos tokens do
    // modelo pedido (o que respondeu vem com sufixo de versao e nao esta na tabela).
    [Fact]
    public void ComUsuarioECustoNulo_UsaEstimativaPelosTokens() {
        var semCusto = Registro() with { CustoUsd = null, TokensEntrada = 1_000_000, TokensSaida = 1_000_000 };

        using (EscopoUsoLlm.Abrir(null, null, null, "usuario-1")) {
            var linha = RegistradorUsoLlm.Montar(semCusto, 2m);

            Assert.Null(linha.CustoUsd);
            Assert.Equal((0.049m + 0.098m) * 2m, linha.CustoBrl);
        }
    }

    [Fact]
    public void EstimativaDeModeloDesconhecido_UsaOPrecoMaisAlto() {
        var custo = ProximoTurnoApi.Domain.IAModel.EstimarCustoUsd("modelo/novo", 1_000_000, 0);

        Assert.Equal(ProximoTurnoApi.Domain.IAModel.PRECO_DESCONHECIDO.Entrada, custo);
    }
}
