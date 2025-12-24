using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IJogoRepository : IBaseRepository {
    Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro);
    Task<List<Jogo>> GetAllByIdsAsync(List<int> ids);
    Task<Jogo?> GetByIdAsync(int id);
    Task SaveAsync(Jogo jogo, bool saveChanges = true);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

public class JogoRepository : BaseRepository, IJogoRepository {

    public JogoRepository(DatabaseContext context) : base(context) {

    }

    public async Task<List<Jogo>> GetAllAsync(FiltroJogoDTO filtro) {
        var query = _dbContext.Jogos.AsQueryable();

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

        return await query
            .Include(j => j.Categoria)
            .Include(j => j.Tags)
            .Include(j => j.Links)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Jogo?> GetByIdAsync(int id) {
        return await _dbContext.Jogos
            .Include(j => j.Tags)
            .Include(j => j.Links)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task SaveAsync(Jogo jogo, bool saveChanges = true) {
        if (jogo.Id == 0) {
            _dbContext.Jogos.Add(jogo);
        } else {
            _dbContext.Jogos.Update(jogo);
        }
        if (!saveChanges)
            return;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Jogos
            .Where(j => j.Id == id)
            .ExecuteDeleteAsync() > 0;
    }

    public Task<bool> ExistsAsync(int id) {
        return _dbContext.Jogos.AnyAsync(j => j.Id == id);
    }

    public Task<List<Jogo>> GetAllByIdsAsync(List<int> ids) {
        return _dbContext.Jogos
            .Where(j => ids.Contains(j.Id))
            //Nao quero carregar todos os dados do jogo
            .Select(j => new Jogo {
                Id = j.Id,
                Nome = j.Nome,
                Status = j.Status
            })
            .ToListAsync();
    }
}
