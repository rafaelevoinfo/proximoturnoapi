using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class PedidoCupomExtensaoPrazoTests {
    private static FakeCategoriaPeriodoCache CriarCache() => new FakeCategoriaPeriodoCache()
        .Adicionar(idPeriodo: 1, quantidadeDias: 2, valor: 20m, idCategoria: 1)
        .Adicionar(idPeriodo: 2, quantidadeDias: 7, valor: 50m, idCategoria: 1)
        .Adicionar(idPeriodo: 3, quantidadeDias: 3, valor: 30m, idCategoria: 2);

    private static Pedido CriarPedido(params (int idJogo, int idCategoria, int idPeriodo, decimal valor)[] itens) {
        var cliente = new Cliente { Id = 1, Nome = "C", Email = "e", Telefone = "t", Endereco = "a" };
        var pedido = new Pedido(cliente, "dinheiro", "retirada");
        foreach (var (idJogo, idCategoria, idPeriodo, valor) in itens) {
            pedido.AdicionarItem(new ItemPedido {
                IdPeriodo = idPeriodo,
                Valor = valor,
                JogoCopia = new JogoCopia { Id = idJogo, IdJogo = idJogo, Status = StatusJogo.Disponivel, Jogo = new Jogo { Id = idJogo, Nome = $"J{idJogo}", IdCategoria = idCategoria } },
                DataDevolucao = pedido.CalcularDataDevolucao(7)
            });
        }
        return pedido;
    }

    private static DateTime FimDoDia(DateTime data) => data.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

    [Fact]
    public void AplicarCupomExtensaoPrazo_TrocaPeriodoPeloMenorDaCategoriaEFixaADevolucao() {
        var pedido = CriarPedido((10, 1, 2, 50m), (20, 2, 3, 30m));
        var data = DateTime.Now.Date.AddDays(15);

        pedido.AplicarCupomExtensaoPrazo(idCupom: 9, data, CriarCache());

        Assert.True(pedido.IsValid);
        Assert.Equal(9, pedido.IdCupom);
        Assert.Equal(0m, pedido.ValorDesconto);
        var jogo10 = pedido.Items.Single(i => i.JogoCopia.IdJogo == 10);
        Assert.Equal(1, jogo10.IdPeriodo);
        Assert.Equal(20m, jogo10.Valor);
        Assert.All(pedido.Items, i => Assert.Equal(FimDoDia(data), i.DataDevolucao));
        Assert.Equal(50m, pedido.ValorTotal); // 20 (menor da categoria 1) + 30
    }

    [Fact]
    public void AplicarCupomExtensaoPrazo_ComCupomJaAplicado_NaoAlteraItens() {
        var pedido = CriarPedido((10, 1, 2, 50m));
        pedido.AplicarCupom(1, 5m);

        pedido.AplicarCupomExtensaoPrazo(idCupom: 9, DateTime.Now.Date.AddDays(15), CriarCache());

        Assert.False(pedido.IsValid);
        Assert.Equal(1, pedido.IdCupom);
        Assert.Equal(2, pedido.Items.Single().IdPeriodo);
    }

    [Fact]
    public void AplicarCupomExtensaoPrazo_CategoriaSemPeriodo_Falha() {
        var pedido = CriarPedido((10, 99, 2, 50m));

        pedido.AplicarCupomExtensaoPrazo(idCupom: 9, DateTime.Now.Date.AddDays(15), new FakeCategoriaPeriodoCache());

        Assert.False(pedido.IsValid);
        Assert.Null(pedido.IdCupom);
    }

    [Fact]
    public void Entregar_ComCupomDeExtensaoPrazo_MantemADataDoCupom() {
        var pedido = CriarPedido((10, 1, 1, 20m));
        var data = DateTime.Now.Date.AddDays(15);
        DefinirCupom(pedido, new Cupom { Id = 9, Codigo = "PRAZO", TipoDesconto = TipoDesconto.ExtensaoPrazo, DataDevolucao = data });

        Assert.True(pedido.Entregar(CriarCache()));

        Assert.Equal(FimDoDia(data), pedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public void Entregar_ComCupomDeExtensaoPrazoVencido_UsaOPeriodo() {
        var pedido = CriarPedido((10, 1, 1, 20m));
        DefinirCupom(pedido, new Cupom { Id = 9, Codigo = "PRAZO", TipoDesconto = TipoDesconto.ExtensaoPrazo, DataDevolucao = DateTime.Now.Date });

        Assert.True(pedido.Entregar(CriarCache()));

        Assert.Equal(FimDoDia(DateTime.Now.AddDays(2)), pedido.Items.Single().DataDevolucao);
    }

    // A navegação Cupom só é preenchida pelo EF ao carregar o pedido.
    private static void DefinirCupom(Pedido pedido, Cupom cupom) =>
        typeof(Pedido).GetProperty(nameof(Pedido.Cupom))!.SetValue(pedido, cupom);
}
