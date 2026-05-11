using Flunt.Notifications;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusPedido(IPedidoRepository pedidoRepository, ILogger<AtualizarStatusPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(int idPedido, StatusPedido novoStatus) {
        logger.LogInformation("Solicitada alteração de status do pedido {PedidoId} para {NovoStatus}.", idPedido, novoStatus);
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            logger.LogWarning("Falha ao atualizar status: Pedido {PedidoId} não encontrado.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        if (novoStatus == StatusPedido.Entregue) {
            if (!pedidoExistente.Entregar()) {
                logger.LogWarning("Regra de negócio impediu entrega do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", pedidoExistente.Notifications.Select(n => n.Message)));
                AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                return;
            }
        } else if (novoStatus == StatusPedido.Cancelado) {
            if (!pedidoExistente.Cancelar()) {
                logger.LogWarning("Regra de negócio impediu cancelamento do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", pedidoExistente.Notifications.Select(n => n.Message)));
                AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                return;
            }
        }
        
        try {
            await _pedidoRepository.SaveAsync(pedidoExistente);
            logger.LogInformation("Status do pedido {PedidoId} atualizado com sucesso para {NovoStatus}.", idPedido, novoStatus);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar alteração de status do pedido {PedidoId}.", idPedido);
            throw;
        }
    }

}