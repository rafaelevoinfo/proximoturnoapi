using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterComentariosPedido(
    DatabaseContext dbContext,
    IClienteRepository clienteRepository,
    UserManager<Usuario> userManager,
    ILogger<ObterComentariosPedido> logger) : UseCaseBasico
{
    public async Task<List<ComentarioDTO>> ExecuteAsync(ClaimsPrincipal userClaim, int pedidoId)
    {
        logger.LogInformation("Buscando comentários do pedido {PedidoId}.", pedidoId);

        var pedido = await dbContext.Pedidos
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == pedidoId);

        if (pedido is null)
        {
            logger.LogWarning("Pedido {PedidoId} não encontrado.", pedidoId);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Pedido não encontrado."));
            return [];
        }

        if (!userClaim.IsInRole(Roles.Admin))
        {
            var user = await userManager.GetUserAsync(userClaim);
            var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
            if (idCliente is null || idCliente.Value != pedido.Cliente.Id)
            {
                logger.LogWarning("Acesso negado: Usuário tentou listar comentários do pedido {PedidoId} pertencente a outro cliente.", pedidoId);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Acesso negado aos comentários deste pedido."));
                return [];
            }
        }

        var comentarios = await dbContext.Comentarios
            .Include(c => c.Cliente)
            .Where(c => c.IdPedido == pedidoId)
            .ToListAsync();

        return comentarios.Select(ComentarioDTO.FromModel).ToList();
    }
}
