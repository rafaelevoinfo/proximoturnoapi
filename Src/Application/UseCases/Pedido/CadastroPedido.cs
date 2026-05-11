using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using Sprache;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroPedido(IPedidoRepository pedidoRepository,
    IJogoRepository _jogoRepository,
    IClienteRepository _clienteRepository,
    ICategoriaRepository _categoriaRepository,
    UserManager<Usuario> _userManager,
    ILogger<CadastroPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task<int> ExecuteAsync(ClaimsPrincipal userClaim, NovoPedidoDTO novoPedidoDto) {
        logger.LogInformation("Iniciando cadastro de novo pedido para o usuário logado.");
        var user = await _userManager.GetUserAsync(userClaim);
        var cliente = await _clienteRepository.GetByEmailAsync(user?.Email ?? "");
        if (cliente is null) {
            logger.LogWarning("Falha ao cadastrar pedido: Usuário {UserEmail} não está vinculado a nenhum cliente.", user?.Email ?? "desconhecido");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não logado ou não vinculado a nenhum cliente."));
            return 0;
        }

        var pedido = new Pedido(cliente);
        foreach (var item in novoPedidoDto.Items) {
            var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _categoriaRepository);
            if (!IsValid) {
                logger.LogWarning("Falha na validação do item do pedido para o jogo ID {JogoId}.", item.IdJogo);
                return 0;
            }

            var itemPedido = new ItemPedido() {
                JogoCopia = resultValidacao.Value.copia!,
                IdPeriodo = resultValidacao.Value.periodo.Id,
                Valor = resultValidacao.Value.periodo.Valor,
                DataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias)
            };

            if (!pedido.AdicionarItem(itemPedido)) {
                logger.LogWarning("Regra de negócio impediu adição de item ao pedido: {Errors}", string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                var notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                AddNotifications((IList<UseCaseNotification>)notifications);
                return 0;
            }
        }

        try {
            await _pedidoRepository.SaveAsync(pedido);
            logger.LogInformation("Pedido {PedidoId} cadastrado com sucesso para o cliente {ClienteId}.", pedido.Id, cliente.Id);
            return pedido.Id;
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar o pedido no banco de dados.");
            throw;
        }
    }

    private async Task<int?> BuscarIdClienteLogado(string email) {
        return await _clienteRepository.GetIdByEmailAsync(email);
    }
}