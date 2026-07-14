using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CancelarPedido(IPedidoRepository pedidoRepository, ILogger<CancelarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(int idPedido) {
        logger.LogInformation("Solicitado cancelamento do pedido {PedidoId}.", idPedido);
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            logger.LogWarning("Falha ao cancelar: Pedido {PedidoId} não encontrado.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        if (!pedidoExistente.Cancelar()) {
            logger.LogWarning("Regra de negócio impediu cancelamento do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", pedidoExistente.Notifications.Select(n => n.Message)));
            AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
            return;
        }

        try {
            await _pedidoRepository.SaveAsync(pedidoExistente);
            logger.LogInformation("Pedido {PedidoId} cancelado com sucesso.", idPedido);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar cancelamento do pedido {PedidoId}.", idPedido);
            throw;
        }
    }
}
