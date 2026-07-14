using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class EntregarPedidoUseCaseTests {
    private static (EntregarPedido useCase, FakePedidoRepository repo) Criar(FakeCategoriaPeriodoCache cache) {
        var repo = new FakePedidoRepository();
        return (new EntregarPedido(repo, cache, new FakeLogger<EntregarPedido>()), repo);
    }

    private static FakePedidoRepository ComPedidoPendente(FakePedidoRepository repo, out JogoCopia copia, int qtdeDias = 7) {
        copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedido = PedidoTestFactory.PedidoPendenteComItem(PedidoTestFactory.Cliente(), copia, idPeriodo: 10, valor: 50m, qtdeDias: qtdeDias, idPedido: 1);
        repo.Pedidos.Add(pedido);
        return repo;
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoExiste_AdicionaNotificacao() {
        var (useCase, repo) = Criar(new FakeCategoriaPeriodoCache());

        await useCase.ExecuteAsync(999);

        Assert.False(useCase.IsValid);
        Assert.Equal(0, repo.SaveCount);
        Assert.Contains(useCase.Notifications, n => n.Message == "Pedido não encontrado.");
    }

    [Fact]
    public async Task ExecuteAsync_SemData_RecalculaComBaseNaEntregaESalva() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo) = Criar(cache);
        ComPedidoPendente(repo, out _, qtdeDias: 7);

        await useCase.ExecuteAsync(1);

        Assert.True(useCase.IsValid);
        Assert.Equal(1, repo.SaveCount);
        var pedido = repo.Pedidos.Single();
        Assert.Equal(StatusPedido.Entregue, pedido.Status);
        var esperado = DateTime.Now.Date.AddDays(7).AddHours(23).AddMinutes(59).AddSeconds(59);
        Assert.Equal(esperado, pedido.Items.Single().DataDevolucao);
    }

    [Fact]
    public async Task ExecuteAsync_ComDataFutura_AplicaDataInformada() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo) = Criar(cache);
        ComPedidoPendente(repo, out _);
        var data = DateTime.Now.Date.AddDays(15);

        await useCase.ExecuteAsync(1, data);

        Assert.True(useCase.IsValid);
        var esperado = data.AddHours(23).AddMinutes(59).AddSeconds(59);
        Assert.Equal(esperado, repo.Pedidos.Single().Items.Single().DataDevolucao);
    }

    [Fact]
    public async Task ExecuteAsync_ComDataNoPassado_FalhaENaoSalva() {
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var (useCase, repo) = Criar(cache);
        ComPedidoPendente(repo, out _);

        await useCase.ExecuteAsync(1, DateTime.Now.Date);

        Assert.False(useCase.IsValid);
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(StatusPedido.Pendente, repo.Pedidos.Single().Status);
    }
}
