using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Data;
using ProximoTurnoApi.Models;
using System.Collections.Generic;
using System.Linq;

namespace ProximoTurnoApi.Repositories;

public interface IClienteRepository {
    Task<List<Cliente>> GetAll();
    Task<Cliente?> GetById(int id);
    Task<Cliente> Add(Cliente cliente);
    Task Update(Cliente cliente);
    Task Delete(int id);
}

public class ClienteRepository : IClienteRepository {
    private readonly ProximoTurnoDbContext _context;

    public ClienteRepository(ProximoTurnoDbContext context) {
        _context = context;
    }

    public async Task<List<Cliente>> GetAll() {
        return await _context.Clientes.ToListAsync();
    }

    public async Task<Cliente?> GetById(int id) {
        return await _context.Clientes.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente> Add(Cliente cliente) {
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task Update(Cliente cliente) {
        _context.Clientes.Update(cliente);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id) {
        var cliente = await GetById(id);
        if (cliente != null) {
            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();
        }
    }
}
