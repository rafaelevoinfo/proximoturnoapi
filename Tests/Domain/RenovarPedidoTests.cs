using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class RenovarPedidoTests {
    private static (RenovarPedido useCase, FakePedidoRepository repo, FakeContratoQueue queue) Criar(FakeCategoriaPeriodoCache cache) {
        var repo = new FakePedidoRepository();
        var queue = new FakeContratoQueue();
        return (new RenovarPedido(repo, cache, queue, new FakeLogger<RenovarPedido>()), repo, queue);
    }

    private static Pedido PedidoEntregue(FakeCategoriaPeriodoCache cache, int idPedido = 1) {
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedido = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(), copia, idPeriodo: 10, valor: 50m, qtdeDias: 7, idItem: 1, idPedido: idPedido);
        pedido.Entregar(cache);
        return pedido;
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoExiste_AdicionaNotificacao() {
        var (useCase, _, _) = Criar(new FakeCategoriaPeriodoCache());

        await useCase.ExecuteAsync(999, [new ItemPedidoRenovarDTO { Id = 1 }]);

        Assert.False(useCase.IsValid);
        Assert.Contains(useCase.Notifications, n => n.Message == "Pedido não encontrado.");
    }

    [Fact]
    public async Task ExecuteAsync_QuandoNenhumItemInformado_AdicionaNotificacao() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo, _) = Criar(cache);
        repo.Pedidos.Add(PedidoEntregue(cache, 1));

        await useCase.ExecuteAsync(1, []);

        Assert.False(useCase.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPeriodoInexistenteNoCache_AdicionaNotificacao() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo, _) = Criar(cache);
        repo.Pedidos.Add(PedidoEntregue(cache, 1));

        // Solicita renovação com um período que não existe no cache
        await useCase.ExecuteAsync(1, [new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = 999 }]);

        Assert.False(useCase.IsValid);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoValido_GeraNovoPedidoSalvaEEnfileiraContrato() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo, queue) = Criar(cache);
        repo.Pedidos.Add(PedidoEntregue(cache, 1));

        await useCase.ExecuteAsync(1, [new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = null }]);

        Assert.True(useCase.IsValid);
        // pedido original (devolvido) + novo pedido (renovado)
        Assert.Equal(2, repo.SaveCount);
        Assert.Single(queue.Enfileirados);
        Assert.Contains(repo.Pedidos, p => p.PedidoOriginal != null && p.Status == StatusPedido.Entregue);
    }
}
