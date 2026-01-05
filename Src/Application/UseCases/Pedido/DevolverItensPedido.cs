using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class DevolverItensPedido(IPedidoRepository _pedidoRepository) : UseCaseBasico {

    public async Task<bool> ExecuteAsync(int idPedido, List<int>? idsItensDevolvidos) {
        var pedido = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedido is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Pedido de id {idPedido} não encontrado."));
            return false;
        }

        if (!pedido.Devolver(idsItensDevolvidos)) {
            AddNotifications((IList<UseCaseNotification>)pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
            return false;
        }
        await _pedidoRepository.SaveAsync(pedido);
        return IsValid;
    }
}
