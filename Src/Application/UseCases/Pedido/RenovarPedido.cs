using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class RenovarPedido(IPedidoRepository _pedidoRepository, IJogoRepository _jogoRepository, IClienteRepository _clienteRepository) : PedidoUseCaseBasico(_pedidoRepository, _jogoRepository, _clienteRepository) {
    public async Task ExecuteAsync(int idPedido, List<ItemPedidoDTO> itens) {
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        var novoPedido = new Pedido() {
            IdCliente = pedidoExistente.IdCliente,
            DataHora = DateTime.Now,
            Status = StatusPedido.Entregue,
            Items = itens.Select(i => new ItemPedido() {
                IdJogo = i.Jogo!.Id,
                Valor = i.Valor.GetValueOrDefault(),
                DataDevolucao = i.DataDevolucao.GetValueOrDefault()
            }).ToList()
        };
        novoPedido.ValorTotal = novoPedido.Items!.Sum(i => i.Valor);

        //pegar os itens que nao estao na lista de itens a serem renovados e marcar os que estão como renovados. 
        var jogosDevolvidos = new List<ItemPedido>();
        foreach (var item in pedidoExistente.Items) {
            if (!itens.Any(i => i.Jogo!.Id == item.IdJogo)) {
                jogosDevolvidos.Add(item);
            } else {
                item.Renovado = true;
            }
        }

        await _pedidoRepository.SaveAsync(novoPedido, false);
        await _pedidoRepository.SaveAsync(pedidoExistente, false);
        await AtualizarStatusJogos(novoPedido.Items, StatusJogo.Alugado);//teoricamente nao precisa pq já estao alugados, mas vamos garantir
        if (jogosDevolvidos.Count > 0) {
            await AtualizarStatusJogos(jogosDevolvidos, StatusJogo.Disponivel);
        }

        await _pedidoRepository.SaveChangesAsync();
    }
}