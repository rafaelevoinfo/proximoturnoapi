using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class RenovarPedido(IPedidoRepository pedidoRepository,

                           ICategoriaRepository _categoriaRepository,
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

        var categorias = await _categoriaRepository.GetAllAsync(new FiltroCategoriaDTO());

        List<(int idItem, CategoriaPeriodo periodo)?> itensNovoPedido = [];
        foreach (var itemRenovar in itens) {
            var itemPedido = pedidoExistente.Items.FirstOrDefault(i => i.Id == itemRenovar.Id);
            if (itemPedido is not null) {
                var novoItemDto = new NovoItemPedidoDTO() {
                    IdCopiaJogo = itemPedido.IdJogoCopia,
                    IdJogo = itemPedido.JogoCopia.IdJogo,
                    IdPeriodo = itemRenovar.IdPeriodo ?? itemPedido.IdPeriodo
                };
                var periodo = categorias.FirstOrDefault(c => c.Periodos.Any(p => p.Id == novoItemDto.IdPeriodo))?.Periodos.FirstOrDefault(p => p.Id == novoItemDto.IdPeriodo);
                if (periodo is null) {
                    logger.LogWarning("Período de renovação ID {PeriodoId} inválido para o item {ItemId} do pedido {PedidoId}.", novoItemDto.IdPeriodo, itemRenovar.Id, idPedido);
                    AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Não foi possível identificar qual o periodo para renovação."));
                    continue;
                }

                itensNovoPedido.Add((itemRenovar.Id, periodo));
            } else {
                logger.LogWarning("Item ID {ItemId} não pertence ao pedido {PedidoId}.", itemRenovar.Id, idPedido);
            }
        }

        if (itensNovoPedido.Count == 0) {
            logger.LogWarning("Nenhum item válido foi processado para renovação do pedido {PedidoId}.", idPedido);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Nenhum item foi informado para ser renovado"));
            return;
        }

        var idsItensDevolucao = pedidoExistente.Items.Select(i => i.Id).Except(itensNovoPedido.Select(i => i!.Value.idItem)).ToList();
        if (!pedidoExistente.Devolver(idsItensDevolucao)) {
            logger.LogWarning("Falha ao processar devolução necessária para renovação do pedido {PedidoId}: {Errors}", idPedido, string.Join(", ", pedidoExistente.Notifications.Select(n => n.Message)));
            AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
            return;
        }

        var novoPedido = pedidoExistente.Renovar(itensNovoPedido);
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