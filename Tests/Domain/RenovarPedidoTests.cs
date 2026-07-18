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

    private static Pedido PedidoEntregueComDoisItens(FakeCategoriaPeriodoCache cache, int idPedido = 1) {
        var pedido = new Pedido(PedidoTestFactory.Cliente()) { Id = idPedido };
        pedido.AdicionarItem(new ItemPedido { Id = 1, IdPeriodo = 10, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1) });
        pedido.AdicionarItem(new ItemPedido { Id = 2, IdPeriodo = 10, Valor = 50m, JogoCopia = PedidoTestFactory.Copia(idCopia: 2, idJogo: 6, idCategoria: 1) });
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

    [Fact]
    public async Task ExecuteAsync_RenovacaoParcial_MantemPedidoOriginalEntregue() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo, queue) = Criar(cache);
        repo.Pedidos.Add(PedidoEntregueComDoisItens(cache, 1));

        await useCase.ExecuteAsync(1, [new ItemPedidoRenovarDTO { Id = 1, IdPeriodo = null }]);

        Assert.True(useCase.IsValid);
        var original = repo.Pedidos.First(p => p.Id == 1);
        Assert.Equal(StatusPedido.Entregue, original.Status);              // sobrou o item 2
        Assert.Contains(repo.Pedidos, p => p.PedidoOriginal != null && p.Items.Count == 1 && p.Status == StatusPedido.Entregue);
        Assert.Single(queue.Enfileirados);
    }

    [Fact]
    public async Task ExecuteAsync_ComDataDevolucaoInformada_UsaDataNoItemDoNovoPedido() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo, _) = Criar(cache);
        repo.Pedidos.Add(PedidoEntregue(cache, 1));
        var dataEscolhida = DateTime.Now.Date.AddDays(45);

        await useCase.ExecuteAsync(1, [new ItemPedidoRenovarDTO { Id = 1, DataDevolucao = dataEscolhida }]);

        Assert.True(useCase.IsValid);
        var novoPedido = repo.Pedidos.Single(p => p.PedidoOriginal != null);
        Assert.Equal(dataEscolhida.AddHours(23).AddMinutes(59).AddSeconds(59), novoPedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public async Task ExecuteAsync_ComDataDevolucaoNoPassado_IgnoraItemEAdicionaNotificacao() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo, _) = Criar(cache);
        repo.Pedidos.Add(PedidoEntregue(cache, 1));

        await useCase.ExecuteAsync(1, [new ItemPedidoRenovarDTO { Id = 1, DataDevolucao = DateTime.Now.Date }]);

        Assert.False(useCase.IsValid);
        Assert.Contains(useCase.Notifications, n => n.Message == "A data de devolução informada deve ser superior à data atual.");
    }
}
