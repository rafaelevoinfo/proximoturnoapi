using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IClienteRepository {
    Task<List<Cliente>> GetAllAsync(FiltroClienteDTO filtro);
    Task<Cliente?> GetByIdAsync(int id);
    Task AddAsync(Cliente cliente);
    Task UpdateAsync(Cliente cliente);
    Task<bool> DeleteAsync(int id);

}

public class ClienteRepository : IClienteRepository {
    private readonly DatabaseContext _dbContext;

    public ClienteRepository(DatabaseContext context) {
        _dbContext = context;
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

        return await query.ToListAsync();
    }

    public async Task<Cliente?> GetByIdAsync(int id) {
        return await _dbContext.Clientes.FirstOrDefaultAsync(c => c.Id == id);
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
            .ExecuteDeleteAsync() > 0;
    }
}