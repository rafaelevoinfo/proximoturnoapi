using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class DevolverItensPedido(IPedidoRepository pedidoRepository, ILogger<DevolverItensPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task<bool> ExecuteAsync(int idPedido, List<int>? idsItensDevolvidos) {
        logger.LogInformation("Recebida solicitação de devolução para o pedido {PedidoId}. Itens: {ItemCount}", idPedido, idsItensDevolvidos?.Count ?? 0);
        var pedido = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedido is null) {
            logger.LogWarning("Falha na devolução: Pedido {PedidoId} não encontrado.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Pedido de id {idPedido} não encontrado."));
            return false;
        }

        if (!pedido.Devolver(idsItensDevolvidos)) {
            logger.LogWarning("Regra de negócio impediu devolução de itens do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", pedido.Notifications.Select(n => n.Message)));
            AddNotifications((IList<UseCaseNotification>)pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
            return false;
        }
        
        try {
            await _pedidoRepository.SaveAsync(pedido);
            logger.LogInformation("Devolução do pedido {PedidoId} processada com sucesso.", idPedido);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar devolução do pedido {PedidoId}.", idPedido);
            throw;
        }
        return IsValid;
    }
}
