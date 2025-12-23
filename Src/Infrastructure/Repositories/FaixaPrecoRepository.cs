using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IFaixaPrecoRepository
{
    Task<List<FaixaPreco>> GetAllAsync();
    Task<FaixaPreco?> GetByIdAsync(int id);
    Task AddAsync(FaixaPreco faixaPreco);
    Task UpdateAsync(FaixaPreco faixaPreco);
    Task<bool> DeleteAsync(int id);
}

public class FaixaPrecoRepository : BaseRepository, IFaixaPrecoRepository
{
    public FaixaPrecoRepository(DatabaseContext context) : base(context)
    {
    }

    public async Task<List<FaixaPreco>> GetAllAsync()
    {
        return await _dbContext.FaixasPreco.ToListAsync();
    }

    public async Task<FaixaPreco?> GetByIdAsync(int id)
    {
        return await _dbContext.FaixasPreco.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(FaixaPreco faixaPreco)
    {
        _dbContext.FaixasPreco.Add(faixaPreco);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(FaixaPreco faixaPreco)
    {
        _dbContext.Entry(faixaPreco).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _dbContext.FaixasPreco
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync() > 0;
    }
}
