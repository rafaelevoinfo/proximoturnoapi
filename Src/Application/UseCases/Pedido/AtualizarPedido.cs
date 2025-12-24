using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarPedido(IPedidoRepository _pedidoRepository, IClienteRepository _clienteRepository, IJogoRepository _jogoRepository) : PedidoUseCaseBasico(_pedidoRepository, _jogoRepository, _clienteRepository) {

    public async Task ExecuteAsync(NovoPedidoDTO novoPedidoDto) {

        if (!await ValidarDados(novoPedidoDto.IdCliente, novoPedidoDto.Items!.Select(i => i.Jogo!.Id).ToList())) {
            return;
        }

        var pedidoExistente = await _pedidoRepository.GetByIdAsync(novoPedidoDto.Id.GetValueOrDefault());
        if (pedidoExistente is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        for (var i = pedidoExistente.Items.Count - 1; i >= 0; i--) {
            var itemExistente = pedidoExistente.Items[i];
            if (!novoPedidoDto.Items.Any(novoItem => novoItem.Id == itemExistente.Id)) {
                pedidoExistente.Items.RemoveAt(i);
            }
        }
        foreach (var item in novoPedidoDto.Items!) {
            var itemExistente = pedidoExistente.Items.FirstOrDefault(pi => pi.Id == item.Id);
            if (itemExistente != null) {
                itemExistente.Valor = item.Valor.GetValueOrDefault();
                itemExistente.DataDevolucao = item.DataDevolucao.GetValueOrDefault();
            } else {
                pedidoExistente.Items.Add(new ItemPedido {
                    IdJogo = item.Jogo!.Id,
                    Valor = item.Valor.GetValueOrDefault(),
                    DataDevolucao = item.DataDevolucao.GetValueOrDefault(),
                });
            }
        }

        pedidoExistente.ValorTotal = novoPedidoDto.Items!.Sum(i => i.Valor.GetValueOrDefault());
        await _pedidoRepository.SaveAsync(pedidoExistente, true);
    }
}