using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IClienteRepository : IBaseRepository {
    Task<List<Cliente>> GetAllAsync(FiltroClienteDTO filtro);
    Task<Cliente?> GetByIdAsync(int id);
    Task<int?> GetIdByEmailAsync(string email);
    Task<Cliente?> GetByEmailAsync(string email);
    Task AddAsync(Cliente cliente);
    Task UpdateAsync(Cliente cliente);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);

}

public class ClienteRepository : BaseRepository, IClienteRepository {

    public ClienteRepository(DatabaseContext context) : base(context) {
    }

    public async Task<List<Cliente>> GetAllAsync(FiltroClienteDTO filtro) {
        var query = _dbContext.Clientes.AsQueryable();

        if (!string.IsNullOrEmpty(filtro.Nome)) {
            query = query.Where(c => c.Nome.Contains(filtro.Nome.ToLowerInvariant()));
        }

        if (!string.IsNullOrEmpty(filtro.Email)) {
            query = query.Where(c => c.Email.Contains(filtro.Email.ToLowerInvariant()));
        }

        if (!string.IsNullOrEmpty(filtro.Telefone)) {
            query = query.Where(c => c.Telefone == filtro.Telefone);
        }

        if (filtro.ApenasAtivos.HasValue && filtro.ApenasAtivos.Value) {
            query = query.Where(c => c.Ativo);
        }

        if (filtro.Page.HasValue && filtro.PageSize.HasValue && filtro.Page.Value > 0 && filtro.PageSize.Value > 0) {
            query = query.Skip((filtro.Page.Value - 1) * filtro.PageSize.Value).Take(filtro.PageSize.Value);
        }

        return await query
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Cliente?> GetByIdAsync(int id) {
        return await _dbContext.Clientes
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<int?> GetIdByEmailAsync(string email) {
        if (string.IsNullOrEmpty(email)) {
            return null;
        }
        return await _dbContext.Clientes
            .Where(c => c.Email == email.ToLowerInvariant())
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<Cliente?> GetByEmailAsync(string email) {
        return await _dbContext.Clientes
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant());
    }

    public async Task AddAsync(Cliente cliente) {
        _dbContext.Clientes.Add(cliente);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Cliente cliente) {
        _dbContext.Entry(cliente).State = EntityState.Modified;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Clientes
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ativo, false)) > 0;
    }

    public Task<bool> ExistsAsync(int id) {
        return _dbContext.Clientes.AnyAsync(c => c.Id == id);
    }
}