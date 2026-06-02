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
    public async Task<ComentarioDTO?> ExecuteAsync(ClaimsPrincipal userClaim, int pedidoId, SalvarComentarioDTO dto)
    {
        logger.LogInformation("Iniciando salvamento de comentário para o pedido {PedidoId}, jogo {JogoId}.", pedidoId, dto.IdJogo);

        var user = await userManager.GetUserAsync(userClaim);
        var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
        if (idCliente is null)
        {
            logger.LogWarning("Usuário não possui cliente vinculado.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não logado ou não vinculado a nenhum cliente."));
            return null;
        }

        var pedido = await dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)
                .ThenInclude(i => i.JogoCopia)
            .FirstOrDefaultAsync(p => p.Id == pedidoId);

        if (pedido is null)
        {
            logger.LogWarning("Pedido {PedidoId} não encontrado.", pedidoId);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, $"Pedido de ID {pedidoId} não encontrado."));
            return null;
        }

        if (pedido.Cliente.Id != idCliente.Value)
        {
            logger.LogWarning("Acesso negado: Pedido {PedidoId} pertence ao cliente {PedidoClienteId}, mas usuário é {UserClienteId}.", pedidoId, pedido.Cliente.Id, idCliente.Value);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Você só pode comentar em seus próprios pedidos."));
            return null;
        }

        if (pedido.Status != StatusPedido.Devolvido)
        {
            logger.LogWarning("Pedido {PedidoId} tem status {Status}. Apenas pedidos devolvidos podem receber comentários.", pedidoId, pedido.Status);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Comentários são permitidos apenas para pedidos já devolvidos."));
            return null;
        }

        var jogoAlugado = pedido.Items.Any(i => i.JogoCopia.IdJogo == dto.IdJogo);
        if (!jogoAlugado)
        {
            logger.LogWarning("Jogo {JogoId} não foi alugado no pedido {PedidoId}.", dto.IdJogo, pedidoId);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Este jogo não faz parte deste pedido."));
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
            .FirstOrDefaultAsync(c => c.IdPedido == pedidoId && c.IdJogo == dto.IdJogo);

        if (comentarioExistente is null)
        {
            var comentario = new Comentario
            {
                IdPedido = pedidoId,
                IdJogo = dto.IdJogo,
                IdCliente = idCliente.Value,
                Texto = dto.Texto,
                Nota = dto.Nota,
                DataHora = DateTime.Now
            };

            await dbContext.Comentarios.AddAsync(comentario);
            await dbContext.SaveChangesAsync();

            // Reload to get the customer name populated
            var comentarioSalvo = await dbContext.Comentarios
                .Include(c => c.Cliente)
                .FirstAsync(c => c.Id == comentario.Id);

            logger.LogInformation("Novo comentário {ComentarioId} criado com sucesso.", comentario.Id);
            return ComentarioDTO.FromModel(comentarioSalvo);
        }
        else
        {
            comentarioExistente.Texto = dto.Texto;
            comentarioExistente.Nota = dto.Nota;
            comentarioExistente.DataHora = DateTime.Now;

            dbContext.Comentarios.Update(comentarioExistente);
            await dbContext.SaveChangesAsync();

            // Reload to get the customer name populated
            var comentarioSalvo = await dbContext.Comentarios
                .Include(c => c.Cliente)
                .FirstAsync(c => c.Id == comentarioExistente.Id);

            logger.LogInformation("Comentário {ComentarioId} atualizado com sucesso.", comentarioExistente.Id);
            return ComentarioDTO.FromModel(comentarioSalvo);
        }
    }
}
