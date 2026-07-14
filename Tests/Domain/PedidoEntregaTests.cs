using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PedidoEntregaTests {
    // Fake in-test do cache (sem lib de mock)
    private class FakeCache(int qtdeDias) : ICategoriaPeriodoCache {
        public bool TryGetPeriodo(int idPeriodo, out CategoriaPeriodoInfo? info) {
            info = new CategoriaPeriodoInfo(idPeriodo, qtdeDias, 0m, 1, "cat");
            return true;
        }
        public int GetQuantidadeDias(int idPeriodo, int defaultDias = 1) => qtdeDias;
        public Task RefreshAsync() => Task.CompletedTask;
    }

    private static Pedido CriarPedidoComItem(int qtdeDias) {
        var cliente = new Cliente { Id = 1, Nome = "C", Email = "e", Telefone = "t", Endereco = "a" };
        var pedido = new Pedido(cliente, "dinheiro", "retirada");
        var item = new ItemPedido {
            Id = 1,
            IdPeriodo = 10,
            Valor = 50m,
            JogoCopia = new JogoCopia { Id = 1, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = 5, Nome = "J", IdCategoria = 1 } },
            DataDevolucao = pedido.CalcularDataDevolucao(qtdeDias) // simula cálculo no cadastro (base = DataHora)
        };
        pedido.AdicionarItem(item);
        return pedido;
    }

    [Fact]
    public void Entregar_SemData_RecalculaDataDevolucaoComBaseNaEntrega() {
        var pedido = CriarPedidoComItem(qtdeDias: 5);
        var cache = new FakeCache(5);

        Assert.True(pedido.Entregar(cache));

        var esperado = DateTime.Now.Date.AddDays(5).AddHours(23).AddMinutes(59).AddSeconds(59);
        Assert.Equal(esperado, pedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public void Entregar_ComDataValida_AplicaMesmaDataATodosOsItens() {
        var pedido = CriarPedidoComItem(qtdeDias: 5);
        var cache = new FakeCache(5);
        var data = DateTime.Now.Date.AddDays(10);

        Assert.True(pedido.Entregar(cache, data));

        var esperado = data.AddHours(23).AddMinutes(59).AddSeconds(59);
        Assert.Equal(esperado, pedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public void Entregar_ComDataNoPassado_FalhaComNotificacao() {
        var pedido = CriarPedidoComItem(qtdeDias: 5);
        var cache = new FakeCache(5);

        var ok = pedido.Entregar(cache, DateTime.Now.Date); // hoje: inválido (não é > hoje)

        Assert.False(ok);
        Assert.False(pedido.IsValid);
        Assert.Equal(StatusPedido.Pendente, pedido.Status);
    }
}
