using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Repositories;

public interface ICategoriaRepository {
    Task<List<Categoria>> GetAllAsync();
    Task<Categoria?> GetByIdAsync(int id);
    Task AddAsync(Categoria categoria);
    Task UpdateAsync(Categoria categoria);
    Task DeleteAsync(int id);
}

public class CategoriaRepository : ICategoriaRepository {
    private readonly DatabaseContext _context;

    public CategoriaRepository(DatabaseContext context) {
        _context = context;
    }

    public async Task<List<Categoria>> GetAllAsync() {
        return await _context.Categorias.ToListAsync();
    }

    public async Task<Categoria?> GetByIdAsync(int id) {
        return await _context.Categorias.FindAsync(id);
    }

    public async Task AddAsync(Categoria categoria) {
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Categoria categoria) {
        _context.Entry(categoria).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id) {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria != null) {
            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();
        }
    }
}