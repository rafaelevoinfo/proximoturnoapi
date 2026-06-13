using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ExcluirCupom(ICupomRepository _repository, ILogger<ExcluirCupom> logger) : UseCaseBasico
{
    public async Task<bool> ExecuteAsync(int id)
    {
        logger.LogInformation("Iniciando exclusão do cupom {CupomId}.", id);

        var cupom = await _repository.GetByIdAsync(id);
        if (cupom == null)
        {
            logger.LogWarning("Cupom {CupomId} não encontrado.", id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Cupom não encontrado."));
            return false;
        }

        var isUsed = await _repository.IsUsedInPedidoAsync(id);
        if (isUsed)
        {
            logger.LogWarning("Falha ao excluir cupom {CupomId}: já está vinculado a pedidos.", id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Este cupom já foi utilizado em pedidos e não pode ser excluído."));
            return false;
        }

        await _repository.DeleteAsync(id);
        logger.LogInformation("Cupom {CupomId} excluído com sucesso.", id);
        return true;
    }
}
