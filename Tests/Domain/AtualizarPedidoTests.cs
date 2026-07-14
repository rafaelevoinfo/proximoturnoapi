using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class AtualizarPedidoTests {
    private static AtualizarPedido Criar(FakePedidoRepository pedidoRepo, FakeJogoRepository jogoRepo, FakeCategoriaPeriodoCache cache, FakeContratoQueue queue) {
        var validarCupom = new ValidarCupom(null!, null!, null!, new FakeLogger<ValidarCupom>());
        return new AtualizarPedido(pedidoRepo, jogoRepo, cache, validarCupom, queue, new FakeLogger<AtualizarPedido>());
    }

    [Fact]
    public async Task ExecuteAsync_QuandoPedidoNaoExiste_AdicionaNotificacao() {
        var pedidoRepo = new FakePedidoRepository();
        var useCase = Criar(pedidoRepo, new FakeJogoRepository(), new FakeCategoriaPeriodoCache(), new FakeContratoQueue());

        await useCase.ExecuteAsync(new NovoPedidoDTO { Id = 999, Items = [] });

        Assert.False(useCase.IsValid);
        Assert.Equal(0, pedidoRepo.SaveCount);
        Assert.Contains(useCase.Notifications, n => n.Message == "Pedido não encontrado.");
    }

    [Fact]
    public async Task ExecuteAsync_QuandoAdicionaNovoItem_AtualizaSalvaEEnfileira() {
        var cliente = PedidoTestFactory.Cliente();
        var pedido = new Pedido(cliente) { Id = 1 };
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);

        var pedidoRepo = new FakePedidoRepository { Pedidos = { pedido } };
        var jogoRepo = new FakeJogoRepository { Copias = { copia }, Jogos = { copia.Jogo } };
        var cache = new FakeCategoriaPeriodoCache().Adicionar(idPeriodo: 10, quantidadeDias: 7, valor: 50m, idCategoria: 1);
        var queue = new FakeContratoQueue();

        var useCase = Criar(pedidoRepo, jogoRepo, cache, queue);

        var dto = new NovoPedidoDTO {
            Id = 1,
            Items = [new NovoItemPedidoDTO { IdJogo = 5, IdCopiaJogo = 1, IdPeriodo = 10 }]
        };

        await useCase.ExecuteAsync(dto);

        Assert.True(useCase.IsValid);
        Assert.Equal(1, pedidoRepo.SaveCount);
        Assert.Contains(1, queue.Enfileirados);
        Assert.Single(pedido.Items);
    }
}
