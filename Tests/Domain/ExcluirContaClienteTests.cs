using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;
using ProximoTurnoApi.Tests.Fakes;

namespace ProximoTurnoApi.Tests.Domain;

public class ExcluirContaClienteTests {

    private static Cliente NovoCliente(int id = 1) => new() {
        Id = id,
        Nome = "ana silva",
        Email = "ana@x.com",
        Telefone = "11999998888",
        Endereco = "rua das flores, 10",
        Cpf = "12345678901",
        DataNascimento = new DateOnly(1990, 5, 20),
        ComoNosConheceu = "instagram",
        AceitaReceberOfertas = true,
        Ativo = true
    };

    private static (ExcluirContaCliente useCase,
                    FakeClienteRepository clientes,
                    FakePedidoRepository pedidos,
                    FakeContratoRepository contratos,
                    FakeUserManager users,
                    FakeEmailService email) Montar(
        Cliente? cliente = null,
        bool isAdmin = false) {

        cliente ??= NovoCliente();
        var clientes = new FakeClienteRepository { Clientes = { cliente } };
        var pedidos = new FakePedidoRepository();
        var contratos = new FakeContratoRepository();
        var usuario = new Usuario { Id = "u1", Email = cliente.Email, Nome = cliente.Nome };
        var users = new FakeUserManager(usuario, isAdmin);
        var email = new FakeEmailService();

        var useCase = new ExcluirContaCliente(
            clientes, pedidos, contratos, users, email,
            NullLogger<ExcluirContaCliente>.Instance);

        return (useCase, clientes, pedidos, contratos, users, email);
    }

    // Pedido.Status tem setter privado. O caminho é o mesmo de PedidoTests.cs: montar o pedido
    // pelo domínio. Nunca abra o setter só para o teste.
    private static Pedido PedidoPendente(Cliente cliente, int idItem = 1) {
        var pedido = new Pedido(cliente);
        pedido.AdicionarItem(new ItemPedido {
            Id = idItem,
            JogoCopia = new JogoCopia {
                Id = idItem,
                Status = StatusJogo.Disponivel,
                Jogo = new Jogo { Id = 10, Nome = "Catan", IdCategoria = 1 }
            },
            IdPeriodo = 1,
            Valor = 50m
        });
        return pedido;
    }

    private static Pedido PedidoEntregue(Cliente cliente, int idItem = 1) {
        var pedido = PedidoPendente(cliente, idItem);
        pedido.Entregar(new FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));
        return pedido;
    }

    private static Pedido PedidoDevolvido(Cliente cliente, int idItem = 1) {
        var pedido = PedidoEntregue(cliente, idItem);
        pedido.Devolver(null);
        return pedido;
    }

    /// <summary>
    /// Pedido entregue com dois jogos, dos quais só o Catan voltou. O pedido segue "Entregue"
    /// porque o Wingspan continua com o cliente. AdicionarItem recusa duplicata por
    /// JogoCopia.IdJogo, então os dois itens precisam de IdJogo distintos.
    /// </summary>
    private static Pedido PedidoParcialmenteDevolvido(Cliente cliente) {
        var pedido = new Pedido(cliente);
        pedido.AdicionarItem(new ItemPedido {
            Id = 1,
            JogoCopia = new JogoCopia {
                Id = 1,
                IdJogo = 10,
                Status = StatusJogo.Disponivel,
                Jogo = new Jogo { Id = 10, Nome = "Catan", IdCategoria = 1 }
            },
            IdPeriodo = 1,
            Valor = 50m
        });
        pedido.AdicionarItem(new ItemPedido {
            Id = 2,
            JogoCopia = new JogoCopia {
                Id = 2,
                IdJogo = 11,
                Status = StatusJogo.Disponivel,
                Jogo = new Jogo { Id = 11, Nome = "Wingspan", IdCategoria = 1 }
            },
            IdPeriodo = 1,
            Valor = 50m
        });
        pedido.Entregar(new FakeCategoriaPeriodoCache().Adicionar(1, 7, 50m, 1));
        pedido.Devolver([1]);
        return pedido;
    }

    // FakeEmailService de EnviarEmailsClientesTests é uma classe privada aninhada e não pode ser
    // reusada aqui. Esta é a cópia local, com a flag de falha que o teste de SMTP precisa.
    private class FakeEmailService : IEmailService {
        public List<(string to, string subject, string body)> Enviados { get; } = [];
        public bool LancarErro { get; set; }

        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true) {
            if (LancarErro) {
                throw new InvalidOperationException("smtp fora do ar");
            }
            Enviados.Add((toEmail, subject, body));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Recusa_QuandoClienteNaoExiste() {
        var (useCase, _, _, _, _, _) = Montar();

        var ok = await useCase.ExecuteAsync(999, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.NotFound);
    }

    [Fact]
    public async Task Idempotente_QuandoJaAnonimizado() {
        var cliente = NovoCliente();
        var dataOriginal = new DateTime(2026, 1, 1);
        cliente.DataAnonimizacao = dataOriginal;
        cliente.Ativo = false;
        var (useCase, _, _, _, _, email) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.True(ok);
        Assert.Empty(useCase.Notifications);
        // Discriminante: se a guarda de idempotência fosse removida, o caminho normal
        // reanonimizaria (mudando DataAnonimizacao) e reenviaria o e-mail de confirmação.
        Assert.Empty(email.Enviados);
        Assert.Equal(dataOriginal, cliente.DataAnonimizacao);
    }

    [Fact]
    public async Task Recusa_QuandoSenhaIncorreta() {
        var (useCase, _, _, _, users, _) = Montar();
        users.SenhaCorreta = false;

        var ok = await useCase.ExecuteAsync(1, "errada", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.BadRequest);
    }

    [Fact]
    public async Task NaoPedeSenha_QuandoSolicitadoPorAdmin() {
        var (useCase, _, _, _, users, _) = Montar();
        users.SenhaCorreta = false;

        var ok = await useCase.ExecuteAsync(1, senha: null, solicitadoPorAdmin: true, idUsuarioAtor: "admin-1");

        Assert.True(ok);
    }

    [Fact]
    public async Task Recusa_QuandoClienteImportadoSemUsuario_SolicitadoPeloProprioTitular() {
        // Cliente importado nunca definiu senha (sem login no Identity). Sem admin, isso
        // tem que falhar fechado na guarda de senha — não há como autenticar o titular.
        var (useCase, _, _, _, users, _) = Montar();
        users.UsuarioPorEmail = null;

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        // Pina que a recusa veio especificamente da guarda de senha, não de qualquer guarda.
        Assert.Contains(useCase.Notifications, n => n.Message == "Senha incorreta.");
    }

    [Fact]
    public async Task Permite_QuandoClienteImportadoSemUsuario_SolicitadoPorAdmin() {
        // Sem usuarioCliente, a guarda de admin (usuarioCliente is not null && ...) é
        // curto-circuitada e a exclusão segue sem Identity user para deletar.
        var (useCase, _, _, _, users, _) = Montar();
        users.UsuarioPorEmail = null;

        var ok = await useCase.ExecuteAsync(1, senha: null, solicitadoPorAdmin: true, idUsuarioAtor: "admin-1");

        Assert.True(ok);
    }

    // Cópia literal de ExcluirContaCliente.MensagemAdmin (privada na classe de produção).
    // Comparar a mensagem inteira, não uma substring, para pegar reescritas da segunda frase.
    private const string MensagemAdminEsperada =
        "Contas com perfil de administrador não podem ser excluídas por aqui. " +
        "Peça a outro administrador para remover seu perfil de administrador e repita a exclusão.";

    // Cópia literal da mensagem de pedidos em aberto em ExcluirContaCliente.ExecuteAsync.
    private const string MensagemPedidosAbertosEsperada =
        "Existem pedidos em aberto. Devolva os jogos antes de excluir a conta.";

    [Fact]
    public async Task Recusa_QuandoAlvoEhAdmin() {
        var (useCase, _, _, _, _, _) = Montar(isAdmin: true);

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemAdminEsperada);
    }

    [Fact]
    public async Task Recusa_ComPedidoPendente_MesmoQuandoTambemEhAdmin() {
        // Pinning de ordem: a guarda de pedidos em aberto (4) roda antes da guarda de
        // administrador (5). Se as duas fossem trocadas de lugar, a recusa aqui viraria
        // a mensagem de admin em vez da de pedidos, e este teste pegaria a regressão.
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente, isAdmin: true);
        pedidos.Pedidos.Add(PedidoPendente(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Single(useCase.PedidosEmAberto);
        Assert.DoesNotContain(useCase.Notifications, n => n.Message == MensagemAdminEsperada);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemPedidosAbertosEsperada);
    }

    [Fact]
    public async Task Recusa_ComPedidoPendente() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoPendente(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Single(useCase.PedidosEmAberto);
        Assert.Contains("Catan", useCase.PedidosEmAberto[0].Jogos);
    }

    [Fact]
    public async Task Recusa_ComPedidoParcialmenteDevolvido_ListaSoOsJogosAindaComOCliente() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoParcialmenteDevolvido(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        var pedido = Assert.Single(useCase.PedidosEmAberto);
        Assert.Equal(["Wingspan"], pedido.Jogos);
        Assert.DoesNotContain("Catan", pedido.Jogos);
    }

    [Fact]
    public async Task Recusa_ComPedidoEntregue() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoEntregue(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Single(useCase.PedidosEmAberto);
    }

    [Fact]
    public async Task Permite_QuandoTodosOsPedidosForamDevolvidos() {
        var cliente = NovoCliente();
        var (useCase, _, pedidos, _, _, _) = Montar(cliente);
        pedidos.Pedidos.Add(PedidoDevolvido(cliente));

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.True(ok);
        Assert.Empty(useCase.PedidosEmAberto);
    }

    [Fact]
    public async Task Permite_QuandoSemPedidoNenhum() {
        var (useCase, _, _, _, _, _) = Montar();

        var ok = await useCase.ExecuteAsync(1, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.True(ok);
        Assert.Empty(useCase.PedidosEmAberto);
    }

    [Fact]
    public async Task Anonimiza_TodosOsCamposPessoais() {
        var cliente = NovoCliente(7);
        var (useCase, _, _, _, _, _) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(7, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.True(ok);
        Assert.Equal("cliente removido", cliente.Nome);
        Assert.Equal("anon-7@removido.local", cliente.Email);
        Assert.Equal("anon7", cliente.Telefone);
        Assert.Equal("removido", cliente.Endereco);
        Assert.Null(cliente.Cpf);
        Assert.Null(cliente.DataNascimento);
        Assert.Null(cliente.ComoNosConheceu);
        Assert.False(cliente.AceitaReceberOfertas);
        Assert.False(cliente.Ativo);
        Assert.NotNull(cliente.DataAnonimizacao);
    }

    [Fact]
    public async Task GeraTokensUnicos_ParaClientesDiferentes() {
        var a = NovoCliente(10);
        var b = NovoCliente(11);
        b.Email = "b@x.com";
        b.Telefone = "11888887777";
        b.Cpf = "98765432100";

        var (useCaseA, _, _, _, _, _) = Montar(a);
        await useCaseA.ExecuteAsync(10, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        var (useCaseB, _, _, _, _, _) = Montar(b);
        await useCaseB.ExecuteAsync(11, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.NotEqual(a.Email, b.Email);
        Assert.NotEqual(a.Telefone, b.Telefone);
    }

    [Fact]
    public async Task Apaga_ComentariosListaDesejosEContratos() {
        var (useCase, clientes, _, contratos, _, _) = Montar(NovoCliente(3));

        await useCase.ExecuteAsync(3, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.Contains(3, clientes.DadosVinculadosExcluidos);
        Assert.Contains(3, contratos.ContratosExcluidosPorCliente);
    }

    [Fact]
    public async Task Deleta_UsuarioDoIdentity() {
        var (useCase, _, _, _, users, _) = Montar(NovoCliente(4));

        await useCase.ExecuteAsync(4, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.Contains("u1", users.Deletados);
    }

    [Fact]
    public async Task EnviaEmailDeConfirmacao_ParaOEnderecoReal() {
        var (useCase, _, _, _, _, email) = Montar(NovoCliente(5));

        await useCase.ExecuteAsync(5, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.Single(email.Enviados);
        Assert.Equal("ana@x.com", email.Enviados[0].to);
        // Pina a outra metade da captura-antes-de-anonimizar: o nome real no corpo do
        // e-mail, não "cliente removido" (o que aconteceria se nomeReal fosse capturado
        // depois de Anonimizar).
        Assert.Contains("ana silva", email.Enviados[0].body);
    }

    [Fact]
    public async Task FalhaNoEmail_NaoDesfazAExclusao() {
        var cliente = NovoCliente(6);
        var (useCase, _, _, _, _, email) = Montar(cliente);
        email.LancarErro = true;

        var ok = await useCase.ExecuteAsync(6, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.True(ok);
        Assert.NotNull(cliente.DataAnonimizacao);
    }

    [Fact]
    public async Task Sucesso_AbreTransacao_AtualizaClienteEFazCommit_SemRollback() {
        var (useCase, clientes, _, _, _, _) = Montar(NovoCliente(8));

        var ok = await useCase.ExecuteAsync(8, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.True(ok);
        Assert.Equal(new[] { "Start", "Update", "Commit" }, clientes.Chamadas);
    }

    [Fact]
    public async Task Recusa_QuandoIdentityFalhaAoDeletar_FazRollback() {
        var cliente = NovoCliente(9);
        var (useCase, clientes, _, _, users, _) = Montar(cliente);
        users.FalharDelete = true;

        var ok = await useCase.ExecuteAsync(9, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1");

        Assert.False(ok);
        Assert.Equal(new[] { "Start", "Update", "Rollback" }, clientes.Chamadas);
        Assert.Contains(useCase.Notifications, n => n.Type == UseCaseNotificationType.Error);
    }

    [Fact]
    public async Task ExcecaoNoMeioDaTransacao_FazRollbackEPropaga() {
        var cliente = NovoCliente(12);
        var (useCase, clientes, _, _, _, _) = Montar(cliente);
        clientes.LancarErroAoExcluirDadosVinculados = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(12, "senha", solicitadoPorAdmin: false, idUsuarioAtor: "u1"));

        Assert.Equal(new[] { "Start", "Rollback" }, clientes.Chamadas);
    }
}
