using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterComentarioPorId(
    DatabaseContext dbContext,
    IClienteRepository clienteRepository,
    UserManager<Usuario> userManager,
    ILogger<ObterComentarioPorId> logger) : UseCaseBasico
{
    public async Task<ComentarioDTO?> ExecuteAsync(ClaimsPrincipal userClaim, int id)
    {
        logger.LogInformation("Buscando comentário {ComentarioId}.", id);

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

        if (!userClaim.IsInRole(Roles.Admin))
        {
            var user = await userManager.GetUserAsync(userClaim);
            var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (idCliente is null || idCliente.Value != comentario.IdCliente)
            {
                logger.LogWarning("Acesso negado: Usuário tentou obter comentário {ComentarioId} de outro cliente.", id);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Acesso negado a este comentário."));
                return null;
            }
        }

        return ComentarioDTO.FromModel(comentario);
    }
}
