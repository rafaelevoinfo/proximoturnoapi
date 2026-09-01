using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Tests.Fakes;

namespace ProximoTurnoApi.Tests.Domain;

/// <summary>
/// Guarda a invariante da decisão 2 da spec de exclusão de conta (LGPD):
/// DataAnonimizacao != null implica Ativo == false. AtualizarStatusCliente é o único caminho
/// de escrita em Cliente.Ativo, então é o único lugar onde a invariante pode ser quebrada.
/// A recíproca não vale: inativo sem DataAnonimizacao é bloqueio comercial, reversível.
/// </summary>
public class AtualizarStatusClienteTests {

    // Cópia literal da mensagem de recusa em AtualizarStatusCliente.ExecuteAsync.
    private const string MensagemContaExcluidaEsperada =
        "Não é possível alterar o status de uma conta excluída.";

    private static Cliente NovoCliente(bool ativo = true, DateTime? dataAnonimizacao = null) => new() {
        Id = 1,
        Nome = "ana silva",
        Email = "ana@x.com",
        Telefone = "11999998888",
        Endereco = "rua das flores, 10",
        Ativo = ativo,
        DataAnonimizacao = dataAnonimizacao
    };

    private static (AtualizarStatusCliente useCase,
                    FakeClienteRepository clientes,
                    FakeUserManager users) Montar(Cliente cliente) {

        var clientes = new FakeClienteRepository { Clientes = { cliente } };
        // Mesmo e-mail do cliente: assim a checagem de posse da conta passa e o caminho feliz
        // chega até o fim mesmo sem perfil de admin.
        var users = new FakeUserManager(new Usuario { Id = "u1", Email = cliente.Email, Nome = cliente.Nome });
        var useCase = new AtualizarStatusCliente(clientes, users, NullLogger<AtualizarStatusCliente>.Instance);

        return (useCase, clientes, users);
    }

    [Fact]
    public async Task Recusa_AtivarContaExcluida() {
        // O cenário real: no grid do admin a conta anonimizada aparecia como "Inativo" e o
        // botão Ativar ressuscitava a conta com DataAnonimizacao ainda preenchida.
        var cliente = NovoCliente(ativo: false, dataAnonimizacao: new DateTime(2026, 1, 1));
        var (useCase, clientes, users) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(1, ativo: true, new ClaimsPrincipal());

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemContaExcluidaEsperada);
        // Discriminantes: sem a guarda, o caminho normal gravaria Ativo = true, abriria a
        // transação e ainda desbloquearia o login no Identity.
        Assert.False(cliente.Ativo);
        Assert.Empty(clientes.Chamadas);
        Assert.False(users.LockoutEndFoiDefinido);
    }

    [Fact]
    public async Task Recusa_InativarContaExcluida() {
        // Recusa nos dois sentidos: "inativar de novo" uma conta já excluída não é operação
        // nenhuma, e deixá-la passar reabriria transação e reescreveria o lockout à toa.
        var cliente = NovoCliente(ativo: false, dataAnonimizacao: new DateTime(2026, 1, 1));
        var (useCase, clientes, users) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(1, ativo: false, new ClaimsPrincipal());

        Assert.False(ok);
        Assert.Contains(useCase.Notifications, n => n.Message == MensagemContaExcluidaEsperada);
        Assert.Empty(clientes.Chamadas);
        Assert.False(users.LockoutEndFoiDefinido);
    }

    [Fact]
    public async Task Permite_InativarClienteSemDataAnonimizacao() {
        // Contraprova de que a guarda não é ampla demais: bloquear por inadimplência continua
        // funcionando e é o estado "inativo mas não excluído".
        var cliente = NovoCliente(ativo: true);
        var (useCase, clientes, users) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(1, ativo: false, new ClaimsPrincipal());

        Assert.True(ok);
        Assert.Empty(useCase.Notifications);
        Assert.False(cliente.Ativo);
        Assert.Null(cliente.DataAnonimizacao);
        Assert.Equal(new[] { "Start", "Update", "Commit" }, clientes.Chamadas);
        Assert.NotNull(users.LockoutEndDefinido);
    }

    [Fact]
    public async Task Permite_ReativarClienteApenasBloqueado() {
        var cliente = NovoCliente(ativo: false);
        var (useCase, clientes, users) = Montar(cliente);

        var ok = await useCase.ExecuteAsync(1, ativo: true, new ClaimsPrincipal());

        Assert.True(ok);
        Assert.Empty(useCase.Notifications);
        Assert.True(cliente.Ativo);
        Assert.Equal(new[] { "Start", "Update", "Commit" }, clientes.Chamadas);
        // Reativar limpa o lockout do Identity.
        Assert.True(users.LockoutEndFoiDefinido);
        Assert.Null(users.LockoutEndDefinido);
    }
}
