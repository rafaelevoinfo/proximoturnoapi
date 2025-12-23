
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface ITagRepository {
    Task<List<Tag>> GetAllAsync(FiltroTagDTO filtro);
    Task<Tag?> GetByIdAsync(int id);
    Task<Tag?> GetByNomeAsync(string nome);
    Task AddAsync(Tag tag);
    Task UpdateAsync(Tag tag);
    Task<bool> DeleteAsync(int id);
}

public class TagRepository : BaseRepository, ITagRepository {
    public TagRepository(DatabaseContext context) : base(context) {
    }

    public async Task<List<Tag>> GetAllAsync(FiltroTagDTO filtro) {
        var query = _dbContext.Tags.AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Nome)) {
            query = query.Where(t => t.Nome.Contains(filtro.Nome.ToLowerInvariant()));
        }

        return await query
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Tag?> GetByIdAsync(int id) {
        return await _dbContext.Tags.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AddAsync(Tag tag) {
        _dbContext.Tags.Add(tag);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Tag tag) {
        _dbContext.Entry(tag).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Tags
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync() > 0;
    }

    public Task<Tag?> GetByNomeAsync(string nome) {
        return _dbContext.Tags.FirstOrDefaultAsync(t => t.Nome == nome);
    }
}
