using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

public interface IPrecoCategoriaRepository {
    Task<List<CategoriaPreco>> GetAllAsync();
    Task<CategoriaPreco?> GetByIdAsync(int id);
    Task AddAsync(CategoriaPreco precoCategoria);
    Task UpdateAsync(CategoriaPreco precoCategoria);
    Task<bool> DeleteAsync(int id);
}

public class PrecoCategoriaRepository : BaseRepository, IPrecoCategoriaRepository {
    public PrecoCategoriaRepository(DatabaseContext context) : base(context) {
    }

    public async Task<List<CategoriaPreco>> GetAllAsync() {
        return await _dbContext.CategoriaPrecos.Include(p => p.Categoria).ToListAsync();
    }

    public async Task<CategoriaPreco?> GetByIdAsync(int id) {
        return await _dbContext.CategoriaPrecos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(CategoriaPreco precoCategoria) {
        _dbContext.CategoriaPrecos.Add(precoCategoria);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(CategoriaPreco precoCategoria) {
        _dbContext.Entry(precoCategoria).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.CategoriaPrecos
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync() > 0;
    }
}