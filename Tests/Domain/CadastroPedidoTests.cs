using System.Security.Claims;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;
using Xunit;

namespace ProximoTurnoApi.Tests.Domain;

public class CadastroPedidoTests {
    private static CadastroPedido Criar(
        FakePedidoRepository pedidoRepo,
        FakeJogoRepository jogoRepo,
        FakeClienteRepository clienteRepo,
        FakeCategoriaPeriodoCache cache,
        FakeUserManager userManager,
        FakeContratoQueue queue,
        FakeNotificationService notificacao,
        FakeConfiguracaoLojaRepository? loja = null) {
        // ValidarCupom não é exercitado (sem CupomCodigo), então dependências podem ser nulas.
        var validarCupom = new ValidarCupom(null!, null!, null!, new FakeLogger<ValidarCupom>());
        return new CadastroPedido(pedidoRepo, jogoRepo, clienteRepo, cache, userManager, validarCupom, queue, notificacao, loja ?? new FakeConfiguracaoLojaRepository(), new FakeHostEnvironment(), new FakeLogger<CadastroPedido>());
    }

    private static ClaimsPrincipal ClaimsVazio() => new(new ClaimsIdentity());

    [Fact]
    public async Task ExecuteAsync_QuandoAdminInformaClienteInexistente_RetornaZero() {
        var pedidoRepo = new FakePedidoRepository();
        var useCase = Criar(pedidoRepo, new FakeJogoRepository(), new FakeClienteRepository(),
            new FakeCategoriaPeriodoCache(), new FakeUserManager(new Usuario { Email = "admin@teste.com" }, isAdmin: true),
            new FakeContratoQueue(), new FakeNotificationService());

        var id = await useCase.ExecuteAsync(ClaimsVazio(), new NovoPedidoDTO { IdCliente = 99, Items = [] });

        Assert.Equal(0, id);
        Assert.False(useCase.IsValid);
        Assert.Equal(0, pedidoRepo.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoUsuarioSemClienteVinculado_RetornaZeroComForbid() {
        var pedidoRepo = new FakePedidoRepository();
        var useCase = Criar(pedidoRepo, new FakeJogoRepository(), new FakeClienteRepository(),
            new FakeCategoriaPeriodoCache(), new FakeUserManager(new Usuario { Email = "semcliente@teste.com" }, isAdmin: false),
            new FakeContratoQueue(), new FakeNotificationService());

        var id = await useCase.ExecuteAsync(ClaimsVazio(), new NovoPedidoDTO { Items = [] });

        Assert.Equal(0, id);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.Forbid);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoValido_CriaPedidoSalvaEnfileiraENotifica() {
        var cliente = PedidoTestFactory.Cliente(id: 1, email: "cliente@teste.com");
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);

        var pedidoRepo = new FakePedidoRepository();
        var jogoRepo = new FakeJogoRepository { Copias = { copia }, Jogos = { copia.Jogo } };
        var clienteRepo = new FakeClienteRepository { Clientes = { cliente } };
        var cache = new FakeCategoriaPeriodoCache().Adicionar(idPeriodo: 10, quantidadeDias: 7, valor: 50m, idCategoria: 1);
        var queue = new FakeContratoQueue();
        var notificacao = new FakeNotificationService();
        var userManager = new FakeUserManager(new Usuario { Email = "cliente@teste.com" }, isAdmin: false);

        var useCase = Criar(pedidoRepo, jogoRepo, clienteRepo, cache, userManager, queue, notificacao);

        var dto = new NovoPedidoDTO {
            Items = [new NovoItemPedidoDTO { IdJogo = 5, IdCopiaJogo = 1, IdPeriodo = 10 }]
        };

        var id = await useCase.ExecuteAsync(ClaimsVazio(), dto);

        Assert.True(useCase.IsValid);
        Assert.NotEqual(0, id);
        Assert.Equal(1, pedidoRepo.SaveCount);
        Assert.Contains(id, queue.Enfileirados);
        Assert.Contains(id, notificacao.Notificados);
    }

    [Fact]
    public async Task ExecuteAsync_QuandoJogoSemCopiaDisponivel_RetornaZero() {
        var cliente = PedidoTestFactory.Cliente(id: 1, email: "cliente@teste.com");
        var copiaAlugada = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1, status: StatusJogo.Alugado);

        var pedidoRepo = new FakePedidoRepository();
        var jogoRepo = new FakeJogoRepository { Copias = { copiaAlugada }, Jogos = { copiaAlugada.Jogo } };
        var clienteRepo = new FakeClienteRepository { Clientes = { cliente } };
        var cache = new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1);
        var userManager = new FakeUserManager(new Usuario { Email = "cliente@teste.com" }, isAdmin: false);

        var useCase = Criar(pedidoRepo, jogoRepo, clienteRepo, cache, userManager, new FakeContratoQueue(), new FakeNotificationService());

        var dto = new NovoPedidoDTO {
            Items = [new NovoItemPedidoDTO { IdJogo = 5, IdCopiaJogo = 1, IdPeriodo = 10 }]
        };

        var id = await useCase.ExecuteAsync(ClaimsVazio(), dto);

        Assert.Equal(0, id);
        Assert.False(useCase.IsValid);
        Assert.Equal(0, pedidoRepo.SaveCount);
    }

    private static (CadastroPedido useCase, FakePedidoRepository repo) CriarComLojaFechada(bool isAdmin) {
        var cliente = PedidoTestFactory.Cliente(id: 1, email: "cliente@teste.com");
        var copia = PedidoTestFactory.Copia(idCopia: 1, idJogo: 5, idCategoria: 1);
        var pedidoRepo = new FakePedidoRepository();
        var useCase = Criar(pedidoRepo,
            new FakeJogoRepository { Copias = { copia }, Jogos = { copia.Jogo } },
            new FakeClienteRepository { Clientes = { cliente } },
            new FakeCategoriaPeriodoCache().Adicionar(10, 7, 50m, 1),
            new FakeUserManager(new Usuario { Email = "cliente@teste.com" }, isAdmin),
            new FakeContratoQueue(), new FakeNotificationService(),
            new FakeConfiguracaoLojaRepository { Configuracao = new ConfiguracaoLoja { Id = 1, LojaFechada = true } });
        return (useCase, pedidoRepo);
    }

    private static NovoPedidoDTO PedidoDoCliente() => new() {
        IdCliente = 1,
        Items = [new NovoItemPedidoDTO { IdJogo = 5, IdCopiaJogo = 1, IdPeriodo = 10 }]
    };

    [Fact]
    public async Task ExecuteAsync_LojaFechada_RecusaPedidoDoCliente() {
        var (useCase, repo) = CriarComLojaFechada(isAdmin: false);

        var id = await useCase.ExecuteAsync(ClaimsVazio(), PedidoDoCliente());

        Assert.Equal(0, id);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.BadRequest && n.Message == CadastroPedido.MensagemLojaFechada);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_LojaFechada_AdminAindaLancaPedido() {
        var (useCase, repo) = CriarComLojaFechada(isAdmin: true);

        var id = await useCase.ExecuteAsync(ClaimsVazio(), PedidoDoCliente());

        Assert.True(useCase.IsValid);
        Assert.NotEqual(0, id);
        Assert.Equal(1, repo.SaveCount);
    }
}
