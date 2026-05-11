using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarPedido(IPedidoRepository pedidoRepository,
    IJogoRepository _jogoRepository,
    ICategoriaRepository _categoriaRepository,
    ILogger<AtualizarPedido> logger) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(NovoPedidoDTO novoPedidoDto) {
        logger.LogInformation("Iniciando atualização do pedido {PedidoId}.", novoPedidoDto.Id);
        var pedido = await _pedidoRepository.GetByIdAsync(novoPedidoDto.Id.GetValueOrDefault());
        if (pedido is null) {
            logger.LogWarning("Falha ao atualizar: Pedido {PedidoId} não encontrado.", novoPedidoDto.Id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        for (var i = pedido.Items.Count - 1; i >= 0; i--) {
            var itemExistente = pedido.Items[i];
            if (!novoPedidoDto.Items.Any(novoItem => novoItem.Id == itemExistente.Id)) {
                if (!pedido.RemoverItem(itemExistente.Id)) {
                    logger.LogWarning("Falha ao remover item {ItemId} do pedido {PedidoId}: {Errors}", itemExistente.Id, pedido.Id, string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                    AddNotifications((IList<UseCaseNotification>)pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                    return;
                }
            }
        }
        foreach (var item in novoPedidoDto.Items!) {
            var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _categoriaRepository);
            if (!IsValid) {
                logger.LogWarning("Falha na validação do item (Jogo ID {JogoId}) durante atualização do pedido {PedidoId}.", item.IdJogo, pedido.Id);
                return;
            }

            var dataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias);
            var itemExistente = pedido.Items.FirstOrDefault(pi => pi.Id == item.Id);
            if (itemExistente != null) {
                if (itemExistente.IdJogoCopia == item.IdCopiaJogo &&
                    itemExistente.Valor == resultValidacao.Value.periodo.Valor &&
                    itemExistente.DataDevolucao == dataDevolucao) {
                    continue;
                }
                pedido.RemoverItem(itemExistente);
            }
            var itemPedido = new ItemPedido() {
                JogoCopia = resultValidacao.Value.copia,
                IdPeriodo = resultValidacao.Value.periodo.Id,
                Valor = resultValidacao.Value.periodo.Valor,
                DataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias)
            };

            if (!pedido.AdicionarItem(itemPedido)) {
                logger.LogWarning("Regra de negócio impediu adição de item ao pedido {PedidoId} durante atualização: {Errors}", pedido.Id, string.Join(", ", pedido.Notifications.Select(n => n.Message)));
                var notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                AddNotifications((IList<UseCaseNotification>)notifications);
                return;
            }
        }

        try {
            await _pedidoRepository.SaveAsync(pedido);
            logger.LogInformation("Pedido {PedidoId} atualizado com sucesso.", pedido.Id);
        } catch (Exception ex) {
            logger.LogError(ex, "Erro fatal ao atualizar o pedido {PedidoId} no banco de dados.", pedido.Id);
            throw;
        }
    }
}