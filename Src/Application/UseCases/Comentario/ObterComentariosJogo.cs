using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterComentariosJogo(
    DatabaseContext dbContext,
    ILogger<ObterComentariosJogo> logger) : UseCaseBasico {
    public async Task<List<ComentarioDTO>> ExecuteAsync(int jogoId, int? qtde = 3) {
        logger.LogInformation("Buscando comentários aprovados para o jogo {JogoId} com limite de {Limite}.", jogoId, qtde);

        var query = dbContext.Comentarios
            .Include(c => c.Cliente)
            .Include(c => c.Jogo)
            .Where(c => c.IdJogo == jogoId && c.Status == StatusComentario.Aprovado)
            .OrderByDescending(c => c.DataHora)
            .AsQueryable();

        if (qtde.HasValue && qtde.Value > 0) {
            query = query.Take(qtde.Value);
        }

        var comentarios = await query.ToListAsync();

        return comentarios.Select(ComentarioDTO.FromModel).ToList();
    }
}
