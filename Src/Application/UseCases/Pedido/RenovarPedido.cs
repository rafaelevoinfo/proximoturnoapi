using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

public class RenovarPedido(IPedidoRepository pedidoRepository,

                           ICategoriaPeriodoCache _categoriaPeriodoCache,
                           IContratoQueue _contratoQueue,
                           ILogger<RenovarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {
    public async Task ExecuteAsync(int idPedido, List<ItemPedidoRenovarDTO> itens) {
        logger.LogInformation("Iniciando renovação para o pedido {PedidoId} com {ItemCount} itens.", idPedido, itens.Count);
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            logger.LogWarning("Falha na renovação: Pedido {PedidoId} não encontrado.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        List<(int idItem, CategoriaPeriodoInfo periodo, DateTime? dataDevolucao)?> itensNovoPedido = [];
        foreach (var itemRenovar in itens) {
            var itemPedido = pedidoExistente.Items.FirstOrDefault(i => i.Id == itemRenovar.Id);
            if (itemPedido is not null) {
                var novoItemDto = new NovoItemPedidoDTO() {
                    IdCopiaJogo = itemPedido.IdJogoCopia,
                    IdJogo = itemPedido.JogoCopia.IdJogo,
                    IdPeriodo = itemRenovar.IdPeriodo ?? itemPedido.IdPeriodo
                };
                if (!_categoriaPeriodoCache.TryGetPeriodo(novoItemDto.IdPeriodo, out var periodo) || periodo is null) {
                    logger.LogWarning("Período de renovação ID {PeriodoId} inválido para o item {ItemId} do pedido {PedidoId}.", novoItemDto.IdPeriodo, itemRenovar.Id, idPedido);
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Não foi possível identificar qual o periodo para renovação."));
                    continue;
                }

                if (itemRenovar.DataDevolucao.HasValue && itemRenovar.DataDevolucao.Value.Date <= DateTime.Now.Date) {
                    logger.LogWarning("Data de devolução informada para o item {ItemId} do pedido {PedidoId} não é futura.", itemRenovar.Id, idPedido);
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A data de devolução informada deve ser superior à data atual."));
                    continue;
                }

                itensNovoPedido.Add((itemRenovar.Id, periodo, itemRenovar.DataDevolucao));
            } else {
                logger.LogWarning("Item ID {ItemId} não pertence ao pedido {PedidoId}.", itemRenovar.Id, idPedido);
            }
        }

        if (itensNovoPedido.Count == 0) {
            logger.LogWarning("Nenhum item válido foi processado para renovação do pedido {PedidoId}.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Nenhum item foi informado para ser renovado"));
            return;
        }

        var novoPedido = pedidoExistente.Renovar(itensNovoPedido, _categoriaPeriodoCache);
        if (novoPedido is null || !pedidoExistente.IsValid || !novoPedido.IsValid) {
            var errors = string.Join(", ", pedidoExistente.Notifications.Concat(novoPedido?.Notifications ?? []).Select(n => n.Message));
            logger.LogWarning("Regra de negócio impediu renovação do pedido {PedidoId}: {Errors}", idPedido, errors);
            var notifications = pedidoExistente.Notifications
                .Concat(novoPedido?.Notifications ?? [])
                .Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message))
                .ToList();
            AddNotifications((IList<UseCaseNotification>)notifications);
            return;
        }

        try {
            await _pedidoRepository.SaveAsync(pedidoExistente, false);
            await _pedidoRepository.SaveAsync(novoPedido);
            logger.LogInformation("Renovação do pedido {PedidoId} concluída. Novo pedido gerado: {NovoPedidoId}.", idPedido, novoPedido.Id);

            _contratoQueue.Enfileirar(novoPedido.Id);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao salvar renovação do pedido {PedidoId}.", idPedido);
            throw;
        }
    }
}