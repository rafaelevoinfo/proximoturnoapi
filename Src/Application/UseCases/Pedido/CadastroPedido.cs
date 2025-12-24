using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroPedido(IPedidoRepository _pedidoRepository, IClienteRepository _clienteRepository, IJogoRepository _jogoRepository) : PedidoUseCaseBasico(_pedidoRepository, _jogoRepository, _clienteRepository) {

    public async Task<int> ExecuteAsync(NovoPedidoDTO novoPedidoDto) {
        if (!await ValidarDados(novoPedidoDto.IdCliente, novoPedidoDto.Items!.Select(i => i.Jogo!.Id).ToList())) {
            return 0;
        }

        var pedido = new Pedido {
            IdCliente = novoPedidoDto.IdCliente,
            DataHora = DateTime.UtcNow,
            ValorTotal = novoPedidoDto.Items!.Sum(i => i.Valor.GetValueOrDefault()),
            Items = novoPedidoDto.Items!.Select(i => new ItemPedido {
                IdJogo = i.Jogo!.Id,
                Valor = i.Valor.GetValueOrDefault(),
                DataDevolucao = i.DataDevolucao.GetValueOrDefault()
            }).ToList()
        };

        await _pedidoRepository.SaveAsync(pedido, false);
        await AtualizarStatusJogos(pedido.Items, StatusJogo.Reservado);
        await _pedidoRepository.SaveChangesAsync();

        return pedido.Id;
    }
}