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
    ILogger<SalvarComentario> logger) : UseCaseBasico {
    public async Task<int?> ExecuteAsync(ClaimsPrincipal userClaim, SalvarComentarioDTO dto) {
        logger.LogInformation("Iniciando salvamento de comentário para o jogo {JogoId}.", dto.IdJogo);

        var user = await userManager.GetUserAsync(userClaim);
        var idCliente = await clienteRepository.GetIdByEmailAsync(user?.Email ?? "");
        if (idCliente is null) {
            logger.LogWarning("Usuário não possui cliente vinculado.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Usuário não logado ou não vinculado a nenhum cliente."));
            return null;
        }

        var pedidosComJogo = await dbContext.Pedidos
            .Where(p => p.Cliente.Id == idCliente.Value && p.Items.Any(i => i.JogoCopia.IdJogo == dto.IdJogo))
            .AsNoTracking()
            .Select(p =>
                new {
                    p.Id,
                    TemItemDevolvido = p.Items.Any(i => i.JogoCopia.IdJogo == dto.IdJogo && i.Status == StatusPedido.Devolvido)
                }
            )
            .ToListAsync();

        if (!pedidosComJogo.Any()) {
            logger.LogWarning("Jogo {JogoId} não foi alugado pelo cliente {ClienteId}.", dto.IdJogo, idCliente.Value);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Você só pode comentar em jogos que já alugou."));
            return null;
        }

        var temPedidoDevolvido = pedidosComJogo.Any(p => p.TemItemDevolvido);
        if (!temPedidoDevolvido) {
            logger.LogWarning("Nenhum pedido do jogo {JogoId} foi devolvido ainda para o cliente {ClienteId}.", dto.IdJogo, idCliente.Value);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Comentários são permitidos apenas para jogos já devolvidos."));
            return null;
        }

        if (dto.Nota < 1 || dto.Nota > 5) {
            logger.LogWarning("Nota {Nota} inválida. Deve ser entre 1 e 5.", dto.Nota);
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "A nota deve ser entre 1 e 5."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Texto) || dto.Texto.Length > 1000) {
            logger.LogWarning("Texto do comentário é vazio ou excede 1000 caracteres.");
            AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O texto do comentário deve conter no máximo 1000 caracteres."));
            return null;
        }

        Comentario? comentario = null;
        if (dto.Id.HasValue) {
            comentario = await dbContext.Comentarios
                .AsTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.Id.Value);
            if (comentario == null) {
                logger.LogWarning("Comentário com id {ComentarioId} não encontrado para edição.", dto.Id.Value);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.NotFound, "Comentário não encontrado para edição."));
                return null;
            }
            if (comentario.IdCliente != idCliente.Value) {
                logger.LogWarning("Cliente {ClienteId} tentou editar comentário {ComentarioId} que pertence a outro cliente.", idCliente.Value, dto.Id.Value);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.Forbid, "Você só pode editar seus próprios comentários."));
                return null;
            }
            if (comentario.IdJogo != dto.IdJogo) {
                logger.LogWarning("Cliente {ClienteId} tentou editar comentário {ComentarioId} com jogo divergente.", idCliente.Value, dto.Id.Value);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "O jogo associado ao comentário não pode ser alterado."));
                return null;
            }
        } else {
            comentario = await dbContext.Comentarios.FirstOrDefaultAsync(c =>
                                      c.IdCliente == idCliente.Value &&
                                      c.IdJogo == dto.IdJogo);

            if (comentario is not null) {
                logger.LogWarning("Comentário já existe para o cliente {ClienteId} e jogo {JogoId}. Informe o ID do comentário para edição.", idCliente.Value, dto.IdJogo);
                AddNotification(UseCaseNotification.Create(UseCaseNotificationType.BadRequest, "Você já comentou sobre este jogo."));
                return null;
            }
        }

        if (comentario is null) {
            comentario = new Comentario {
                IdJogo = dto.IdJogo,
                IdCliente = idCliente.Value,
            };

            await dbContext.Comentarios.AddAsync(comentario);
        }
        comentario.Texto = dto.Texto;
        comentario.Nota = dto.Nota;
        comentario.DataHora = DateTime.Now;
        comentario.Status = StatusComentario.Pendente;

        await dbContext.SaveChangesAsync();

        logger.LogInformation("Comentário {ComentarioId} atualizado com sucesso.", comentario.Id);
        return comentario.Id;
    }
}
