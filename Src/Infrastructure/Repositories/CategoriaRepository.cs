using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;
using ProximoTurnoApi.Infrastructure.Services;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface ICategoriaRepository {
    Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro);
    Task<Categoria?> GetByIdAsync(int id);
    Task SaveAsync(Categoria categoria, bool commit = true);
    Task<bool> DeleteAsync(int id);
}

public class CategoriaRepository : BaseRepository, ICategoriaRepository {
    private readonly ICategoriaPeriodoCache _cache;

    public CategoriaRepository(DatabaseContext context, ICategoriaPeriodoCache cache) : base(context) {
        _cache = cache;
    }

    public async Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro) {
        var query = _dbContext.Categorias.AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Descricao)) {
            query = query.Where(c => c.Descricao.Contains(filtro.Descricao.ToLowerInvariant()));
        }

        if (filtro.ApenasAtivos) {
            query = query.Where(c => c.Ativo);
        }

        return await query
            .Include(c => c.Periodos)
            .ToListAsync();
    }

    public async Task<Categoria?> GetByIdAsync(int id) {
        return await _dbContext.Categorias
            .Include(c => c.Periodos)
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task SaveAsync(Categoria categoria, bool commit = true) {
        await SaveChangesAsync(_dbContext.Categorias, categoria, commit);
        if (commit) {
            await _cache.RefreshAsync();
        }
    }

    public async Task<bool> DeleteAsync(int id) {
        var afetadas = await _dbContext.Categorias
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ativo, false));
        if (afetadas > 0) {
            await _cache.RefreshAsync();
        }
        return afetadas > 0;
    }
}
