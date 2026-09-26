using ProximoTurnoApi.Application.UseCases.Chat;
using ProximoTurnoApi.Application.UseCases.IA;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class ObterSaldoChatTests {

    private static readonly CotacaoUsdBrl Cotacao = new(5m, FonteCotacao.Api, DateTime.Now);

    [Fact]
    public void UsuarioSemAluguel_TemSoOBonusConvertido() {
        var saldo = ObterSaldoChat.Calcular(0m, 0m, Cotacao, ConfiguracaoChat.Padrao, admin: false);

        Assert.Equal(2.5m, saldo.Bonus);
        Assert.Equal(2.5m, saldo.Saldo);
        Assert.True(ObterSaldoChat.PodePerguntar(saldo));
    }

    [Fact]
    public void Credito_EhDezPorCentoDoValorBaseMaisBonusMenosGasto() {
        var saldo = ObterSaldoChat.Calcular(120m, 3m, Cotacao, ConfiguracaoChat.Padrao, admin: false);

        Assert.Equal(12m, saldo.CreditoAlugueis);
        Assert.Equal(12m + 2.5m - 3m, saldo.Saldo);
    }

    [Fact]
    public void SaldoZerado_NaoPodePerguntar() {
        var saldo = ObterSaldoChat.Calcular(0m, 2.5m, Cotacao, ConfiguracaoChat.Padrao, admin: false);

        Assert.Equal(0m, saldo.Saldo);
        Assert.False(ObterSaldoChat.PodePerguntar(saldo));
    }

    [Fact]
    public void Admin_PodePerguntarMesmoComSaldoNegativo() {
        var saldo = ObterSaldoChat.Calcular(0m, 50m, Cotacao, ConfiguracaoChat.Padrao, admin: true);

        Assert.True(saldo.Saldo < 0);
        Assert.True(saldo.Ilimitado);
        Assert.True(ObterSaldoChat.PodePerguntar(saldo));
    }

    [Fact]
    public void ConfiguracaoMudaPercentualEBonus() {
        var saldo = ObterSaldoChat.Calcular(100m, 0m, Cotacao, new ConfiguracaoChat(0.2m, 1m), admin: false);

        Assert.Equal(20m, saldo.CreditoAlugueis);
        Assert.Equal(5m, saldo.Bonus);
    }

    [Fact]
    public async Task ExecuteAsync_SemEmail_NaoSomaAlugueis() {
        var repositorio = new FakeChatRegrasRepository { ValorBaseAlugado = 999m, Gasto = 1m };
        var caso = new ObterSaldoChat(repositorio, new FakeCotacaoDolar(5m), ConfiguracaoChat.Padrao);

        var saldo = await caso.ExecuteAsync("usuario-1", null, admin: false);

        Assert.Equal(0m, saldo.ValorBaseAlugado);
        Assert.Equal(1.5m, saldo.Saldo);
    }
}
