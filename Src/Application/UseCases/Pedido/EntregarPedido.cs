using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class EntregarPedido(IPedidoRepository pedidoRepository, ICategoriaPeriodoCache categoriaPeriodoCache, ILogger<EntregarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(int idPedido, DateTime? dataDevolucao = null) {
        logger.LogInformation("Solicitada entrega do pedido {PedidoId}.", idPedido);
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            logger.LogWarning("Falha ao entregar: Pedido {PedidoId} não encontrado.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        if (!pedidoExistente.Entregar(categoriaPeriodoCache, dataDevolucao)) {
            logger.LogWarning("Regra de negócio impediu entrega do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", pedidoExistente.Notifications.Select(n => n.Message)));
            AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
            return;
        }

        try {
            await _pedidoRepository.SaveAsync(pedidoExistente);
            logger.LogInformation("Pedido {PedidoId} entregue com sucesso.", idPedido);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar entrega do pedido {PedidoId}.", idPedido);
            throw;
        }
    }
}
