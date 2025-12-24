using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Application.UseCases;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class DevolverItensPedido(IJogoRepository _jogoRepository, IPedidoRepository _pedidoRepository) : UseCaseBasico {

    public async Task<bool> ExecuteAsync(int idPedido, List<int>? idsItensDevolvidos) {
        var pedido = await _pedidoRepository.GetByIdAsync(idPedido);
        if (pedido is null) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Error, $"Pedido de id {idPedido} não encontrado."));
            return false;
        }
        var idsJogos = new List<int>();
        if (idsItensDevolvidos is null || idsItensDevolvidos.Count == 0) {
            idsJogos = [.. pedido.Items.Select(i => i.IdJogo)];
        } else {
            idsJogos = pedido.Items
                .Where(i => idsItensDevolvidos.Contains(i.Id))
                .Select(i => i.IdJogo)
                .ToList();
        }

        var jogos = await _jogoRepository.GetAllByIdsAsync(idsJogos);
        foreach (var jogo in jogos) {
            jogo.Status = StatusJogo.Disponivel;
        }
        await _jogoRepository.SaveChangesAsync();
        return IsValid;
    }
}
