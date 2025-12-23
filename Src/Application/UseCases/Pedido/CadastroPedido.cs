using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class CadastroPedido(IPedidoRepository _pedidoRepository, IClienteRepository _clienteRepository, IJogoRepository _jogoRepository) : PedidoUseCaseBasico(_pedidoRepository, _jogoRepository, _clienteRepository) {

    public async Task<int> ExecuteAsync(NovoPedidoDTO novoPedidoDto) {
        if (!await ValidarDados(novoPedidoDto.IdCliente, novoPedidoDto.Items!.Select(i => i.IdJogo).ToList())) {
            return 0;
        }

        var pedido = new Pedido {
            IdCliente = novoPedidoDto.IdCliente,
            DataHora = DateTime.UtcNow,
            ValorTotal = novoPedidoDto.Items!.Sum(i => i.Valor),
            Items = novoPedidoDto.Items!.Select(i => new PedidoJogo {
                IdJogo = i.IdJogo,
                Valor = i.Valor,
                DataDevolucao = i.DataDevolucao,
                Status = PedidoJogoStatus.Alugado
            }).ToList()
        };

        await _pedidoRepository.SaveAsync(pedido, false);
        await AtualizarStatusJogos(pedido.Items);
        await _pedidoRepository.SaveChangesAsync();

        return pedido.Id;
    }
}