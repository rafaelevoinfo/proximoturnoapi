using System.Security.Claims;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class BuscarPedidosTests {
    private static BuscarPedidos Criar(FakePedidoRepository pedidoRepo, FakeClienteRepository clienteRepo, FakeUserManager userManager) {
        return new BuscarPedidos(pedidoRepo, clienteRepo, new FakeContratoRepository(), userManager, new FakeLogger<BuscarPedidos>());
    }

    private static ClaimsPrincipal ComRole(string role) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)]));

    [Fact]
    public async Task ExecuteAsync_QuandoAdmin_RetornaTodosOsPedidos() {
        var pedidoRepo = new FakePedidoRepository {
            Pedidos = {
                new Pedido(PedidoTestFactory.Cliente(1)) { Id = 1 },
                new Pedido(PedidoTestFactory.Cliente(2, "outro@teste.com")) { Id = 2 }
            }
        };
        var useCase = Criar(pedidoRepo, new FakeClienteRepository(), new FakeUserManager(null));

        var resultado = await useCase.ExecuteAsync(ComRole(Roles.Admin), new FiltroPedidoDTO());

        Assert.True(useCase.IsValid);
        Assert.Equal(2, resultado.Count);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoMemberSemClienteVinculado_RetornaVazioComForbid() {
        var useCase = Criar(new FakePedidoRepository(), new FakeClienteRepository(),
            new FakeUserManager(new Usuario { Email = "semcliente@teste.com" }));

        var resultado = await useCase.ExecuteAsync(ComRole(Roles.Member), new FiltroPedidoDTO());

        Assert.Empty(resultado);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.Forbid);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoMember_RetornaApenasPedidosDoProprioCliente() {
        var cliente = PedidoTestFactory.Cliente(id: 1, email: "cliente@teste.com");
        var pedidoRepo = new FakePedidoRepository {
            Pedidos = {
                new Pedido(cliente) { Id = 1 },
                new Pedido(PedidoTestFactory.Cliente(2, "outro@teste.com")) { Id = 2 }
            }
        };
        var clienteRepo = new FakeClienteRepository { Clientes = { cliente } };
        var useCase = Criar(pedidoRepo, clienteRepo, new FakeUserManager(new Usuario { Email = "cliente@teste.com" }));

        var resultado = await useCase.ExecuteAsync(ComRole(Roles.Member), new FiltroPedidoDTO());

        Assert.True(useCase.IsValid);
        Assert.Single(resultado);
        Assert.Equal(1, resultado[0].Id);
    }

    [Fact]
    public async Task ExecuteAsync_PorId_QuandoNaoExiste_RetornaNull() {
        var useCase = Criar(new FakePedidoRepository(), new FakeClienteRepository(), new FakeUserManager(null));

        var resultado = await useCase.ExecuteAsync(ComRole(Roles.Admin), 999);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ExecuteAsync_PorId_QuandoAdmin_RetornaPedido() {
        var pedido = new Pedido(PedidoTestFactory.Cliente(1)) { Id = 1 };
        var pedidoRepo = new FakePedidoRepository { Pedidos = { pedido } };
        var useCase = Criar(pedidoRepo, new FakeClienteRepository(), new FakeUserManager(null));

        var resultado = await useCase.ExecuteAsync(ComRole(Roles.Admin), 1);

        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Id);
    }
}
