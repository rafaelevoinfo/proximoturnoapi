using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class CancelarPedidoTests {
    private static (CancelarPedido useCase, FakePedidoRepository repo) Criar() {
        var repo = new FakePedidoRepository();
        return (new CancelarPedido(repo, new FakeLogger<CancelarPedido>()), repo);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoExiste_AdicionaNotificacaoENaoSalva() {
        var (useCase, repo) = Criar();

        await useCase.ExecuteAsync(999);

        Assert.False(useCase.IsValid);
        Assert.Equal(0, repo.SaveCount);
        Assert.Contains(useCase.Notifications, n => n.Message == "Pedido não encontrado.");
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoPendente_CancelaESalva() {
        var (useCase, repo) = Criar();
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedido = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(), copia, idPeriodo: 10, valor: 50m, qtdeDias: 7, idPedido: 1);
        repo.Pedidos.Add(pedido);

        await useCase.ExecuteAsync(1);

        Assert.True(useCase.IsValid);
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(StatusPedido.Cancelado, pedido.Status);
        Assert.Equal(StatusJogo.Disponivel, copia.Status);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoJaEntregue_NaoCancela() {
        var (useCase, repo) = Criar();
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedido = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(), copia, idPeriodo: 10, valor: 50m, qtdeDias: 7, idPedido: 1);
        pedido.Entregar(new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1));
        repo.Pedidos.Add(pedido);

        await useCase.ExecuteAsync(1);

        Assert.False(useCase.IsValid);
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(StatusPedido.Entregue, pedido.Status);
    }
}
