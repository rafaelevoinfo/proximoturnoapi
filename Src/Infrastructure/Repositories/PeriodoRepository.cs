using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IPeriodoRepository {
    Task<List<Periodo>> GetAllAsync();
    Task<Periodo?> GetByIdAsync(int id);
    Task AddAsync(Periodo faixaPreco);
    Task UpdateAsync(Periodo faixaPreco);
    Task<bool> DeleteAsync(int id);
}

public class PeriodoRepository(DatabaseContext context) : BaseRepository(context), IPeriodoRepository {
    public async Task<List<Periodo>> GetAllAsync() {
        return await _dbContext.FaixasPreco.ToListAsync();
    }

    public async Task<Periodo?> GetByIdAsync(int id) {
        return await _dbContext.FaixasPreco
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(Periodo faixaPreco) {
        _dbContext.FaixasPreco.Add(faixaPreco);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Periodo faixaPreco) {
        _dbContext.Entry(faixaPreco).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.FaixasPreco
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync() > 0;
    }
}
