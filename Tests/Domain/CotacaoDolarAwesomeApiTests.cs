using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Infrastructure.IA;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class CotacaoDolarAwesomeApiTests {

    private const string RespostaOk = """{"USDBRL":{"code":"USD","codein":"BRL","bid":"5.4300","ask":"5.4321"}}""";

    private readonly FakeCotacaoHandler _handler = new() { Corpo = RespostaOk };
    private DateTime _agora = new(2026, 9, 26, 10, 0, 0);

    private CotacaoDolarAwesomeApi Cotacao() =>
        new(NullLogger<CotacaoDolarAwesomeApi>.Instance, new FakeHttpClientFactory(_handler), 5.5m, () => _agora);

    [Fact]
    public async Task ApiOk_DevolveOAsk() {
        var cotacao = await Cotacao().ObterAsync();

        Assert.Equal(5.4321m, cotacao.Valor);
        Assert.Equal(FonteCotacao.Api, cotacao.Fonte);
    }

    [Fact]
    public async Task DentroDaValidade_NaoConsultaDeNovo() {
        var servico = Cotacao();
        await servico.ObterAsync();
        _agora = _agora.AddHours(5);

        await servico.ObterAsync();

        Assert.Equal(1, _handler.Chamadas);
    }

    [Fact]
    public async Task ValidadeVencida_ConsultaDeNovo() {
        var servico = Cotacao();
        await servico.ObterAsync();
        _agora = _agora.AddHours(7);

        await servico.ObterAsync();

        Assert.Equal(2, _handler.Chamadas);
    }

    [Fact]
    public async Task ApiFalhaSemValorAnterior_UsaOPadrao() {
        _handler.Erro = new HttpRequestException("fora do ar");

        var cotacao = await Cotacao().ObterAsync();

        Assert.Equal(5.5m, cotacao.Valor);
        Assert.Equal(FonteCotacao.Padrao, cotacao.Fonte);
    }

    [Fact]
    public async Task ApiFalhaDepoisDeUmValorBom_ReaproveitaOUltimo() {
        var servico = Cotacao();
        await servico.ObterAsync();
        _agora = _agora.AddHours(7);
        _handler.Status = HttpStatusCode.InternalServerError;

        var cotacao = await servico.ObterAsync();

        Assert.Equal(5.4321m, cotacao.Valor);
        Assert.Equal(FonteCotacao.UltimaConhecida, cotacao.Fonte);
    }

    // Com a API fora do ar, cada pergunta do chat pagaria o timeout inteiro.
    [Fact]
    public async Task DepoisDeFalhar_EsperaAntesDeTentarDeNovo() {
        _handler.Erro = new HttpRequestException("fora do ar");
        var servico = Cotacao();
        await servico.ObterAsync();

        _agora = _agora.AddMinutes(5);
        await servico.ObterAsync();
        Assert.Equal(1, _handler.Chamadas);

        _agora = _agora.AddMinutes(6);
        await servico.ObterAsync();
        Assert.Equal(2, _handler.Chamadas);
    }

    [Fact]
    public void AtualSemConsulta_EhOPadrao() {
        Assert.Equal(new CotacaoUsdBrl(5.5m, FonteCotacao.Padrao, _agora), Cotacao().Atual);
    }

    [Theory]
    [InlineData("""{"USDBRL":{"ask":"5.4321"}}""", "5.4321")]
    [InlineData("""{"USDBRL":{"ask":5.1}}""", "5.1")]
    [InlineData("""{"USDBRL":{"ask":"abc"}}""", null)]
    [InlineData("""{"USDBRL":{"ask":"0"}}""", null)]
    [InlineData("""{"USDBRL":{"ask":"540"}}""", null)]
    [InlineData("""{"status":404,"code":"CoinNotExists"}""", null)]
    [InlineData("não é json", null)]
    [InlineData("", null)]
    public void Interpretar(string corpo, string? esperado) {
        Assert.Equal(esperado is null ? null : decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture),
                     CotacaoDolarAwesomeApi.Interpretar(corpo));
    }
}
