using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class PodeComentarJogo(
    DatabaseContext dbContext,
    IClienteRepository clienteRepository,
    UserManager<Usuario> userManager,
    ILogger<PodeComentarJogo> logger) : UseCaseBasico
{
    public async Task<bool> ExecuteAsync(ClaimsPrincipal userClaim, int jogoId)
    {
        logger.LogInformation("Verificando elegibilidade de comentário para o jogo {JogoId}.", jogoId);

        var user = await userManager.GetUserAsync(userClaim);
        var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
        if (idCliente is null)
        {
            logger.LogWarning("Usuário não possui cliente vinculado.");
            return false;
        }

        var jaDevolveu = await dbContext.Pedidos
            .AnyAsync(p => p.Cliente.Id == idCliente.Value &&
                           p.Items.Any(i => i.JogoCopia.IdJogo == jogoId && i.Status == StatusPedido.Devolvido));

        var jaComentou = await dbContext.Comentarios
            .AnyAsync(c => c.IdCliente == idCliente.Value && c.IdJogo == jogoId);

        return jaDevolveu && !jaComentou;
    }
}
