using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class SalvarComentario(
    DatabaseContext dbContext,
    IClienteRepository clienteRepository,
    UserManager<Usuario> userManager,
    ILogger<SalvarComentario> logger) : UseCaseBasico
{
    public async Task<ComentarioDTO?> ExecuteAsync(ClaimsPrincipal userClaim, SalvarComentarioDTO dto)
    {
        logger.LogInformation("Iniciando salvamento de comentário para o jogo {JogoId}.", dto.IdJogo);

        var user = await userManager.GetUserAsync(userClaim);
        var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
        if (idCliente is null)
        {
            logger.LogWarning("Usuário não possui cliente vinculado.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não logado ou não vinculado a nenhum cliente."));
            return null;
        }

        var pedidosComJogo = await dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)
                .ThenInclude(i => i.JogoCopia)
            .Where(p => p.Cliente.Id == idCliente.Value && p.Items.Any(i => i.JogoCopia.IdJogo == dto.IdJogo))
            .ToListAsync();

        if (!pedidosComJogo.Any())
        {
            logger.LogWarning("Jogo {JogoId} não foi alugado pelo cliente {ClienteId}.", dto.IdJogo, idCliente.Value);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Você só pode comentar em jogos que já alugou."));
            return null;
        }

        var temPedidoDevolvido = pedidosComJogo.Any(p => p.Status == StatusPedido.Devolvido);
        if (!temPedidoDevolvido)
        {
            logger.LogWarning("Nenhum pedido do jogo {JogoId} foi devolvido ainda para o cliente {ClienteId}.", dto.IdJogo, idCliente.Value);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Comentários são permitidos apenas para jogos já devolvidos."));
            return null;
        }

        if (dto.Nota < 1 || dto.Nota > 5)
        {
            logger.LogWarning("Nota {Nota} inválida. Deve ser entre 1 e 5.", dto.Nota);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A nota deve ser entre 1 e 5."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Texto) || dto.Texto.Length > 1000)
        {
            logger.LogWarning("Texto do comentário é vazio ou excede 1000 caracteres.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O texto do comentário deve conter no máximo 1000 caracteres."));
            return null;
        }

        var comentarioExistente = await dbContext.Comentarios
            .FirstOrDefaultAsync(c => c.IdCliente == idCliente.Value && c.IdJogo == dto.IdJogo);

        if (comentarioExistente is null)
        {
            var comentario = new Comentario
            {
                IdJogo = dto.IdJogo,
                IdCliente = idCliente.Value,
                Texto = dto.Texto,
                Nota = dto.Nota,
                DataHora = DateTime.Now,
                Status = StatusComentario.Pendente
            };

            await dbContext.Comentarios.AddAsync(comentario);
            await dbContext.SaveChangesAsync();

            // Reload to get the customer name and game name populated
            var comentarioSalvo = await dbContext.Comentarios
                .Include(c => c.Cliente)
                .Include(c => c.Jogo)
                .FirstAsync(c => c.Id == comentario.Id);

            logger.LogInformation("Novo comentário {ComentarioId} criado com sucesso.", comentario.Id);
            return ComentarioDTO.FromModel(comentarioSalvo);
        }
        else
        {
            comentarioExistente.Texto = dto.Texto;
            comentarioExistente.Nota = dto.Nota;
            comentarioExistente.DataHora = DateTime.Now;
            comentarioExistente.Status = StatusComentario.Pendente;

            dbContext.Comentarios.Update(comentarioExistente);
            await dbContext.SaveChangesAsync();

            // Reload to get the customer name and game name populated
            var comentarioSalvo = await dbContext.Comentarios
                .Include(c => c.Cliente)
                .Include(c => c.Jogo)
                .FirstAsync(c => c.Id == comentarioExistente.Id);

            logger.LogInformation("Comentário {ComentarioId} atualizado com sucesso.", comentarioExistente.Id);
            return ComentarioDTO.FromModel(comentarioSalvo);
        }
    }
}
