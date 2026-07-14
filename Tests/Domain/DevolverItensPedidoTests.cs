using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class DevolverItensPedidoTests {
    private static (DevolverItensPedido useCase, FakePedidoRepository repo) Criar() {
        var repo = new FakePedidoRepository();
        return (new DevolverItensPedido(repo, new FakeLogger<DevolverItensPedido>()), repo);
    }

    private static Pedido PedidoEntregue(int idPedido = 1) {
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedido = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(), copia, idPeriodo: 10, valor: 50m, qtdeDias: 7, idPedido: idPedido);
        pedido.Entregar(new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1));
        return pedido;
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoExiste_RetornaFalseComNotificacao() {
        var (useCase, repo) = Criar();

        var resultado = await useCase.ExecuteAsync(999, null);

        Assert.False(resultado);
        Assert.False(useCase.IsValid);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoEntregue_RetornaFalse() {
        var (useCase, repo) = Criar();
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pendente = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(), copia, idPeriodo: 10, valor: 50m, qtdeDias: 7, idPedido: 1);
        repo.Pedidos.Add(pendente);

        var resultado = await useCase.ExecuteAsync(1, null);

        Assert.False(resultado);
        Assert.False(useCase.IsValid);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoEntregue_DevolveESalva() {
        var (useCase, repo) = Criar();
        repo.Pedidos.Add(PedidoEntregue(1));

        var resultado = await useCase.ExecuteAsync(1, null);

        Assert.True(resultado);
        Assert.True(useCase.IsValid);
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(StatusPedido.Devolvido, repo.Pedidos.Single().Status);
    }
}
