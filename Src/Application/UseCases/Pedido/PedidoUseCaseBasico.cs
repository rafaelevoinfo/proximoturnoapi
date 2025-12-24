using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class PedidoUseCaseBasico(IPedidoRepository pedidoRepository, IJogoRepository jogoRepository, IClienteRepository clienteRepository) : UseCaseBasico {
    protected readonly IPedidoRepository _pedidoRepository = pedidoRepository;
    protected readonly IJogoRepository _jogoRepository = jogoRepository;
    protected readonly IClienteRepository _clienteRepository = clienteRepository;

    protected async Task<bool> ValidarDados(int idCliente, List<int> idsJogos) {
        if (idsJogos == null || idsJogos.Count == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O pedido deve conter ao menos um item."));
        }
        if (idCliente <= 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O ID do cliente é obrigatório e deve ser maior que zero."));
        }
        if (!IsValid)
            return false;

        if (!await _clienteRepository.ExistsAsync(idCliente)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Cliente {idCliente} não encontrado."));
            return false;
        }

        foreach (var idJogo in idsJogos!) {
            if (!await _jogoRepository.ExistsAsync(idJogo)) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Jogo {idJogo} não encontrado."));
            }
        }

        return IsValid;
    }

    protected async Task AtualizarStatusJogos(List<ItemPedido> itensPedido, StatusJogo statusJogo) {
        foreach (var item in itensPedido) {
            var jogo = item.Jogo;
            if (jogo is null) {
                jogo = await _jogoRepository.GetByIdAsync(item.IdJogo);
            }
            if (jogo is null) {
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, $"Jogo {item.IdJogo} não encontrado para atualização de status."));
                continue;
            }
            jogo.Status = statusJogo;
            await _jogoRepository.SaveAsync(jogo, false);
        }
    }
}