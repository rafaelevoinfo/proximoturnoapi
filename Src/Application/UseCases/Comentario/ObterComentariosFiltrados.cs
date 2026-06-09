using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Repositories;

namespace ProximoTurnoApi.Application.UseCases;

public class ObterComentariosFiltrados(
    DatabaseContext dbContext,
    ILogger<ObterComentariosFiltrados> logger) : UseCaseBasico {
    public async Task<List<ComentarioDTO>> ExecuteAsync(ComentarioFiltersDTO filters) {
        logger.LogInformation("Buscando comentários com filtros.");

        filters ??= new ComentarioFiltersDTO();

        var query = dbContext.Comentarios
            .Include(c => c.Cliente)
            .Include(c => c.Jogo)
            .AsQueryable();

        // Filtro de Status: se não enviado, padrão é Pendente (0)
        var statusFiltro = filters.Status ?? StatusComentario.Pendente;
        query = query.Where(c => c.Status == statusFiltro);

        if (filters.DataInicial.HasValue) {
            query = query.Where(c => c.DataHora >= filters.DataInicial.Value);
        }

        if (filters.DataFinal.HasValue) {
            var dataFinal = filters.DataFinal.Value.Date.AddDays(1).AddTicks(-1); // Fim do dia
            query = query.Where(c => c.DataHora <= dataFinal);
        }

        var comentarios = await query
            .OrderByDescending(c => c.DataHora)
            .ToListAsync();

        return comentarios.Select(ComentarioDTO.FromModel).ToList();
    }
}
