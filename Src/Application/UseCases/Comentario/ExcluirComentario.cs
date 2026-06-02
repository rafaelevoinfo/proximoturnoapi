using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ExcluirComentario(
    DatabaseContext dbContext,
    IClienteRepository clienteRepository,
    UserManager<Usuario> userManager,
    ILogger<ExcluirComentario> logger) : UseCaseBasico
{
    public async Task<bool> ExecuteAsync(ClaimsPrincipal userClaim, int id)
    {
        logger.LogInformation("Iniciando exclusão do comentário {ComentarioId}.", id);

        var comentario = await dbContext.Comentarios.FindAsync(id);
        if (comentario is null)
        {
            logger.LogWarning("Comentário {ComentarioId} não encontrado.", id);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Comentário não encontrado."));
            return false;
        }

        if (!userClaim.IsInRole(Roles.Admin))
        {
            var user = await userManager.GetUserAsync(userClaim);
            var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (idCliente is null || idCliente.Value != comentario.IdCliente)
            {
                logger.LogWarning("Acesso negado: Usuário tentou excluir comentário {ComentarioId} de outro cliente.", id);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Acesso negado para excluir este comentário."));
                return false;
            }
        }

        dbContext.Comentarios.Remove(comentario);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Comentário {ComentarioId} excluído com sucesso.", id);
        return true;
    }
}
