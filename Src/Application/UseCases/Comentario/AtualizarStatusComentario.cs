using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class AtualizarStatusComentario(
    DatabaseContext dbContext,
    ILogger<AtualizarStatusComentario> logger) : UseCaseBasico
{
    public async Task<ComentarioDTO?> ExecuteAsync(int id, StatusComentario novoStatus)
    {
        logger.LogInformation("Iniciando alteração de status do comentário {ComentarioId} para {Status}.", id, novoStatus);

        if (!Enum.IsDefined(typeof(StatusComentario), novoStatus))
        {
            logger.LogWarning("Status {Status} inválido.", novoStatus);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Status do comentário inválido."));
            return null;
        }

        var comentario = await dbContext.Comentarios
            .Include(c => c.Cliente)
            .Include(c => c.Jogo)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comentario is null)
        {
            logger.LogWarning("Comentário {ComentarioId} não encontrado.", id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Comentário não encontrado."));
            return null;
        }

        comentario.Status = novoStatus;
        dbContext.Comentarios.Update(comentario);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Comentário {ComentarioId} atualizado para o status {Status} com sucesso.", id, novoStatus);
        return ComentarioDTO.FromModel(comentario);
    }
}
