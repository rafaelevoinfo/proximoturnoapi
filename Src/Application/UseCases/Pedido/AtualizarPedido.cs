using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarPedido(IPedidoRepository pedidoRepository,
                             IJogoRepository _jogoRepository,
                             IPeriodoRepository _periodoRepository,
                             ICategoriaRepository _categoriaRepository) : PedidoUseCaseBasico(pedidoRepository) {

    public async Task ExecuteAsync(NovoPedidoDTO novoPedidoDto) {
        var pedido = await _pedidoRepository.GetByIdAsync(novoPedidoDto.Id.GetValueOrDefault());
        if (pedido is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        for (var i = pedido.Items.Count - 1; i >= 0; i--) {
            var itemExistente = pedido.Items[i];
            if (!novoPedidoDto.Items.Any(novoItem => novoItem.Id == itemExistente.Id)) {
                if (!pedido.RemoverItem(itemExistente.Id)) {
                    AddNotifications((IList<UseCaseNotification>)pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
                    return;
                }
            }
        }
        foreach (var item in novoPedidoDto.Items!) {
            var itemExistente = pedido.Items.FirstOrDefault(pi => pi.Id == item.Id);
            if (itemExistente != null) {
                pedido.RemoverItem(itemExistente);
            }

            var resultValidacao = await ValidarAdicionarItem(item, _jogoRepository, _periodoRepository, _categoriaRepository);
            if (!IsValid) {
                return;
            }

            var itemPedido = new ItemPedido() {
                JogoCopia = resultValidacao.Value.copia!,
                Valor = resultValidacao.Value.periodo.Valor,
                DataDevolucao = pedido.CalcularDataDevolucao(resultValidacao.Value.periodo.QuantidadeDias)
            };
            if (!pedido.AdicionarItem(itemPedido)) {
                var notifications = pedido.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList();
                AddNotifications((IList<UseCaseNotification>)notifications);
                return;
            }

        }

        await _pedidoRepository.SaveAsync(pedido);
    }
}