using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>Segura uma cópia do jogo para o carrinho enquanto o cliente não fecha o pedido.</summary>
public class ReservarJogoCarrinho(IJogoRepository _jogoRepository,
                                  IReservasCarrinho _reservas,
                                  ILogger<ReservarJogoCarrinho> _logger) : UseCaseBasico {

    /// <summary>O fim da reserva, ou null com a notificação do motivo.</summary>
    public async Task<DateTimeOffset?> ExecuteAsync(Guid idCarrinho, int idJogo) {
        var copias = await _jogoRepository.GetAllCopiasByIdJogoAsync(idJogo);
        var disponiveis = copias.Count(c => c.Status == StatusJogo.Disponivel);
        if (disponiveis == 0) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Este jogo não está disponível no momento."));
            return null;
        }

        var fim = _reservas.Reservar(idCarrinho, idJogo, disponiveis);
        if (fim is null) {
            _logger.LogInformation("Jogo {IdJogo} sem cópia livre: todas seguradas em outros carrinhos.", idJogo);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest,
                "Este jogo está reservado no carrinho de outro cliente. Tente de novo em alguns minutos."));
            return null;
        }

        _logger.LogInformation("Jogo {IdJogo} reservado para o carrinho {IdCarrinho} até {Fim}.", idJogo, idCarrinho, fim);
        return fim;
    }
}
