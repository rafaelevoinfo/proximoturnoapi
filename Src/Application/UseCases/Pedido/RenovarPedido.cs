using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class RenovarPedido(IPedidoRepository pedidoRepository,
                           IJogoRepository _jogoRepository,
                           IPeriodoRepository _periodoRepository,
                           ICategoriaRepository _categoriaRepository) : PedidoUseCaseBasico(pedidoRepository) {
    public async Task ExecuteAsync(int idPedido, List<ItemPedidoRenovarDTO> itens) {
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        List<(int, Periodo)?> itensNovoPedido = [];
        foreach (var itemRenovar in itens) {
            var itemPedido = pedidoExistente.Items.FirstOrDefault(i => i.Id == itemRenovar.Id);
            if (itemPedido is not null) {
                var novoItemDto = new NovoItemPedidoDTO() {
                    IdCopiaJogo = itemPedido.IdJogoCopia,
                    IdJogo = itemPedido.JogoCopia.IdJogo,
                    IdPeriodo = itemRenovar.IdPeriodo
                };
                var resultValidacao = await ValidarAdicionarItem(novoItemDto, _jogoRepository, _periodoRepository, _categoriaRepository);
                if (!IsValid) {
                    return;
                }

                itensNovoPedido.Add((itemRenovar.Id, resultValidacao.Value.periodo));
            }
        }

        var novoPedido = pedidoExistente.Renovar(itensNovoPedido);
        if (novoPedido is null || !pedidoExistente.IsValid) {
            AddNotifications((IList<UseCaseNotification>)pedidoExistente.Notifications.Select(n => UseCaseNotification.Create(UseCaseNotificationType.BadRequest, n.Message)).ToList());
            return;
        }

        await _pedidoRepository.SaveAsync(pedidoExistente, false);
        await _pedidoRepository.SaveAsync(novoPedido);
    }
}