using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Repositories;

public interface IClienteRepository {
    Task<List<Cliente>> GetAllAsync();
    Task<Cliente?> GetByIdAsync(int id);
    Task AddAsync(Cliente cliente);
    Task UpdateAsync(Cliente cliente);
    Task DeleteAsync(int id);
}

public class ClienteRepository : IClienteRepository {
    private readonly DatabaseContext _context;

    public ClienteRepository(DatabaseContext context) {
        _context = context;
    }

    public async Task<List<Cliente>> GetAllAsync() {
        return await _context.Clientes.ToListAsync();
    }

    public async Task<Cliente?> GetByIdAsync(int id) {
        return await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Cliente cliente) {
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Cliente cliente) {
        _context.Entry(cliente).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id) {
        var cliente = await GetByIdAsync(id);
        if (cliente != null) {
            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
        }
    }
}