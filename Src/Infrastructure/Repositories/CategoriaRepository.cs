using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface ICategoriaRepository {
    Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro);
    Task<Categoria?> GetByIdAsync(int id);
    Task AddAsync(Categoria categoria);
    Task UpdateAsync(Categoria categoria);
    Task<bool> DeleteAsync(int id);
}

public class CategoriaRepository : BaseRepository, ICategoriaRepository {

    public CategoriaRepository(DatabaseContext context) : base(context) {
    }

    public async Task<List<Categoria>> GetAllAsync(FiltroCategoriaDTO filtro) {
        var query = _dbContext.Categorias.AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Descricao)) {
            query = query.Where(c => c.Descricao.Contains(filtro.Descricao.ToLowerInvariant()));
        }

        return await query
            .Include(c => c.FaixasPreco)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Categoria?> GetByIdAsync(int id) {
        return await _dbContext.Categorias
            .Include(c => c.FaixasPreco)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Categoria categoria) {
        _dbContext.Categorias.Add(categoria);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Categoria categoria) {
        _dbContext.Entry(categoria).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Categorias
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync() > 0;
    }
}
