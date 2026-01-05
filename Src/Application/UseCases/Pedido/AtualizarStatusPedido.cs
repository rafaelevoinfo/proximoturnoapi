using Flunt.Notifications;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusPedido(IPedidoRepository pedidoRepository) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(int idPedido, StatusPedido novoStatus) {
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        if (novoStatus == StatusPedido.Entregue) {
            if (!pedidoExistente.Entregar()) {
                AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                return;
            }
        } else if (novoStatus == StatusPedido.Cancelado) {
            if (!pedidoExistente.Cancelar()) {
                AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                return;
            }
        }
        await _pedidoRepository.SaveAsync(pedidoExistente);
    }

}