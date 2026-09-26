using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Atende o direito de eliminação do titular (LGPD Art. 18, VI): anonimiza o cliente,
/// apaga comentários, lista de desejos, contratos locais e o login do Identity.
/// O histórico de pedidos é preservado por obrigação fiscal (Art. 16, I).
/// </summary>
public class ExcluirContaCliente(
    IClienteRepository clienteRepository,
    IPedidoRepository pedidoRepository,
    IContratoRepository contratoRepository,
    UserManager<Usuario> userManager,
    IEmailService emailService,
    ILogger<ExcluirContaCliente> logger) : UseCaseBasico {

    private const string MensagemAdmin =
        "Contas com perfil de administrador não podem ser excluídas por aqui. " +
        "Peça a outro administrador para remover seu perfil de administrador e repita a exclusão.";

    /// <summary>Preenchido apenas quando a recusa foi por pedidos em aberto.</summary>
    public IReadOnlyList<PedidoEmAbertoDTO> PedidosEmAberto { get; private set; } = [];

    /// <param name="idUsuarioAtor">
    /// Id do usuário do Identity que disparou a operação — o próprio titular ou o administrador
    /// que atendeu o pedido. Vai só para o log: a exclusão é irreversível e qualquer admin pode
    /// dispará-la contra qualquer conta sem senha, então o "quem" precisa ficar registrado ao
    /// lado do "quando" (DATA_ANONIMIZACAO, LGPD Art. 37). Null quando não há usuário resolvido.
    /// </param>
    public async Task<bool> ExecuteAsync(int idCliente, string? senha, bool solicitadoPorAdmin, string? idUsuarioAtor) {
        // Nunca logamos nome, e-mail ou CPF do titular: este é justamente o caminho que apaga
        // esses dados, e o log não pode virar a cópia que sobrou. Id + ator + canal bastam.
        logger.LogInformation(
            "Iniciando exclusão de conta do cliente {ClienteId}. Ator: {IdUsuarioAtor}, solicitado por admin: {SolicitadoPorAdmin}.",
            idCliente, idUsuarioAtor ?? "desconhecido", solicitadoPorAdmin);
        PedidosEmAberto = [];

        var cliente = await clienteRepository.GetByIdAsync(idCliente);
        if (cliente is null) {
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.NotFound, $"Cliente de id {idCliente} não encontrado."));
            return false;
        }

        // Idempotente: repetir a exclusão de uma conta já excluída é sucesso, não erro.
        if (cliente.DataAnonimizacao is not null) {
            logger.LogInformation("Cliente {ClienteId} já estava anonimizado.", idCliente);
            return true;
        }

        var usuarioCliente = await userManager.FindByEmailAsync(cliente.Email);

        // Senha só é exigida do próprio titular. A autenticação de admin já é o controle no
        // caminho administrativo. Sem isso, uma sessão sequestrada apagaria a conta.
        if (!solicitadoPorAdmin) {
            if (usuarioCliente is null || string.IsNullOrEmpty(senha)
                || !await userManager.CheckPasswordAsync(usuarioCliente, senha)) {
                AddNotification(UseCaseNotification.Create(
                    UseCaseNotificationType.BadRequest, "Senha incorreta."));
                return false;
            }
        }

        var pedidosAbertos = await pedidoRepository.ObterPedidosEmAbertoAsync(idCliente);
        if (pedidosAbertos.Count > 0) {
            // Só os itens ainda em poder do cliente entram na mensagem. Um pedido continua
            // "Entregue" enquanto sobrar um item entregue, mas os demais podem já ter voltado —
            // por devolução parcial (Pedido.Devolver aceita uma lista de itens) ou por renovação,
            // que fecha a perna antiga como Devolvido. Listar p.Items inteiro mandaria o cliente
            // devolver jogos que ele já devolveu.
            PedidosEmAberto = [.. pedidosAbertos.Select(p => new PedidoEmAbertoDTO(
                p.Id,
                p.DataHora,
                [.. p.Items
                    .Where(i => i.Status == StatusPedido.Pendente || i.Status == StatusPedido.Entregue)
                    .Select(i => i.JogoCopia?.Jogo?.Nome ?? "jogo")]))];

            logger.LogInformation(
                "Exclusão recusada: cliente {ClienteId} tem {Qtde} pedido(s) em aberto.",
                idCliente, pedidosAbertos.Count);
            AddNotification(UseCaseNotification.Create(
                UseCaseNotificationType.BadRequest,
                "Existem pedidos em aberto. Devolva os jogos antes de excluir a conta."));
            return false;
        }

        // Guarda técnica, não isenção de LGPD: deletar o IdentityUser leva junto o vínculo de
        // role, e perder a última conta Admin exige recuperação manual no banco. Ver decisão 6.
        if (usuarioCliente is not null && await userManager.IsInRoleAsync(usuarioCliente, Roles.Admin)) {
            logger.LogWarning("Exclusão recusada: cliente {ClienteId} tem perfil de administrador.", idCliente);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, MensagemAdmin));
            return false;
        }

        // Capturado antes de anonimizar: o e-mail de confirmação vai para o endereço real.
        var emailReal = cliente.Email;
        var nomeReal = cliente.Nome;

        await clienteRepository.StartTransactionAsync();
        try {
            // Todos os repositórios compartilham o mesmo DatabaseContext scoped, e o Identity
            // usa esse mesmo contexto (AddEntityFrameworkStores<DatabaseContext>). Por isso uma
            // transação aberta aqui cobre também os ExecuteDeleteAsync dos outros repositórios
            // e o DeleteAsync do UserManager — não existe conta meio-excluída.
            await clienteRepository.ExcluirDadosVinculadosAsync(idCliente);
            await contratoRepository.ExcluirPorClienteAsync(idCliente);

            Anonimizar(cliente);
            await clienteRepository.UpdateAsync(cliente);

            if (usuarioCliente is not null) {
                var resultado = await userManager.DeleteAsync(usuarioCliente);
                if (!resultado.Succeeded) {
                    foreach (var erro in resultado.Errors) {
                        AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, erro.Description));
                    }
                    await clienteRepository.RollbackTransactionAsync();
                    return false;
                }
            }

            await clienteRepository.CommitTransactionAsync();
        } catch (Exception ex) {
            // Log antes do rollback: se o commit já tiver falhado, o rollback pode lançar em
            // cima de uma transação sem mais o que desfazer, e isso substituiria ex sem log
            // nenhum — o pior momento para perder o motivo real da falha.
            logger.LogError(ex, "Erro ao excluir a conta do cliente {ClienteId}.", idCliente);
            await clienteRepository.RollbackTransactionAsync();
            throw;
        }

        logger.LogInformation(
            "Conta do cliente {ClienteId} excluída e anonimizada. Ator: {IdUsuarioAtor}, solicitado por admin: {SolicitadoPorAdmin}.",
            idCliente, idUsuarioAtor ?? "desconhecido", solicitadoPorAdmin);

        // Fora da transação de propósito: a exclusão já está feita e é a prioridade.
        // Falha de SMTP vira log, nunca rollback.
        try {
            await emailService.SendEmailAsync(
                emailReal,
                "Sua conta na Próximo Turno foi excluída",
                $"Olá, {nomeReal}.<br><br>Sua conta foi excluída e seus dados pessoais foram removidos " +
                "do nosso sistema. Seu histórico de pedidos foi mantido de forma anônima, como exige " +
                "a legislação fiscal.<br><br>Se não foi você quem pediu, entre em contato conosco.");
        } catch (Exception ex) {
            logger.LogError(ex, "Conta {ClienteId} excluída, mas o e-mail de confirmação falhou.", idCliente);
        }

        return true;
    }

    private static void Anonimizar(Cliente cliente) {
        // Tokens com o id embutido são obrigatórios: EMAIL, TELEFONE e CPF são índices UNIQUE.
        cliente.Nome = "cliente removido";
        cliente.Email = $"anon-{cliente.Id}@removido.local";
        cliente.Telefone = $"anon{cliente.Id}";
        cliente.Endereco = "removido";
        cliente.Cpf = null;
        cliente.DataNascimento = null;
        cliente.ComoNosConheceu = null;
        cliente.AceitaReceberOfertas = false;
        cliente.Ativo = false;
        cliente.DataAnonimizacao = DateTime.Now;
    }
}
