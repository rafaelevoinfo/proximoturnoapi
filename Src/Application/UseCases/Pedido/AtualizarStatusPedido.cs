using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusPedido(IPedidoRepository _pedidoRepository, IJogoRepository jogoRepository, IClienteRepository clienteRepository) : PedidoUseCaseBasico(_pedidoRepository, jogoRepository, clienteRepository) {

    public async Task ExecuteAsync(int idPedido, StatusPedido novoStatus) {
        var pedidoExistente = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedidoExistente is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Pedido não encontrado."));
            return;
        }

        if (novoStatus < pedidoExistente.Status) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Não é permitido regredir o status do pedido."));
            return;
        }
        pedidoExistente.Status = novoStatus;

        await _pedidoRepository.SaveAsync(pedidoExistente, false);
        var novoStatusJogo = novoStatus switch {
            StatusPedido.Criado => StatusJogo.Reservado,
            StatusPedido.Entregue => StatusJogo.Alugado,
            StatusPedido.Cancelado => StatusJogo.Disponivel,
            _ => throw new ArgumentOutOfRangeException(nameof(novoStatus), $"Status de pedido '{novoStatus}' não mapeado para status de jogo."),
        };
        await AtualizarStatusJogos(pedidoExistente.Items, novoStatusJogo);

        await _pedidoRepository.SaveChangesAsync();
    }

}