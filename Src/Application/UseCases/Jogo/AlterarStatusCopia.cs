using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

/// <summary>
/// Troca à mão o status de uma cópia entre Disponível, Apenas em eventos e Em manutenção.
/// Reservado e Alugado são do fluxo de pedidos: uma cópia nesses status não pode ser mexida
/// aqui, senão a entrega, o cancelamento ou a devolução sobrescreveriam o status manual.
/// </summary>
public class AlterarStatusCopia(IJogoRepository _repository) : UseCaseBasico {

    public async Task<bool> ExecuteAsync(int idJogo, int idCopia, StatusJogo novoStatus) {
        if (!StatusDoJogo.Manuais.Contains(novoStatus)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest,
                "Status inválido: use Disponível, Apenas em eventos ou Em manutenção."));
            return false;
        }

        var copia = await _repository.GetCopiaByIdAsync(idCopia);
        if (copia is null || copia.IdJogo != idJogo) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, $"Cópia {idCopia} não encontrada."));
            return false;
        }

        if (!StatusDoJogo.Manuais.Contains(copia.Status)) {
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, copia.Status switch {
                StatusJogo.Reservado => "A cópia está reservada num pedido. Mude o status depois que o pedido for cancelado ou devolvido.",
                StatusJogo.Alugado => "A cópia está alugada. Mude o status depois da devolução.",
                StatusJogo.Desativado => "A cópia está desativada. Reative-a antes de mudar o status.",
                _ => "O status atual da cópia não pode ser alterado manualmente.",
            }));
            return false;
        }

        if (copia.Status != novoStatus) {
            copia.Status = novoStatus;
            await _repository.SaveAsync(copia);
        }
        return true;
    }
}
