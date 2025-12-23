using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IJogoRepository {
    Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro);
    Task<Jogo?> GetByIdAsync(int id);
    Task AddAsync(Jogo jogo);
    Task UpdateAsync(Jogo jogo);
    Task<bool> DeleteAsync(int id);
}

public class JogoRepository : BaseRepository, IJogoRepository {

    public JogoRepository(DatabaseContext context) : base(context) {

    }

    public async Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) {
        var query = _dbContext.Jogos.Include(j => j.Categoria).AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Nome)) {
            query = query.Where(j => j.Nome.Contains(filtro.Nome.ToLowerInvariant()));
        }

        if (filtro.IdCategoria.HasValue) {
            query = query.Where(j => j.IdCategoria == filtro.IdCategoria.Value);
        }

        if (filtro.Tags != null && filtro.Tags.Count > 0) {
            query = query.Where(j => j.Tags != null && j.Tags.Any(t => filtro.Tags.Contains(t.Nome)));
        }

        if (filtro.Status.HasValue) {
            query = query.Where(j => j.Status == filtro.Status.Value);
        }

        if (filtro.IdadeMinima.HasValue) {
            query = query.Where(j => j.IdadeMinima <= filtro.IdadeMinima.Value);
        }

        if (filtro.NumeroDeJogadores.HasValue) {
            query = query.Where(j => j.MinimoDeJogadores <= filtro.NumeroDeJogadores.Value && j.MaximoDeJogadores >= filtro.NumeroDeJogadores.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<Jogo?> GetByIdAsync(int id) {
        return await _dbContext.Jogos.Include(j => j.Categoria).FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task AddAsync(Jogo jogo) {
        _dbContext.Jogos.Add(jogo);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Jogo jogo) {
        _dbContext.Entry(jogo).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Jogos
            .Where(j => j.Id == id)
            .ExecuteDeleteAsync() > 0;
    }
}
