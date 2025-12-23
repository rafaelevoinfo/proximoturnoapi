using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;
using ProximoTurnoApi.Infrastructure.Repositories;

public interface IPrecoCategoriaRepository {
    Task<List<CategoriaPreco>> GetAllAsync();
    Task<CategoriaPreco?> GetByIdAsync(int id);
    Task AddAsync(CategoriaPreco precoCategoria);
    Task UpdateAsync(CategoriaPreco precoCategoria);
    Task DeleteAsync(int id);
}

public class PrecoCategoriaRepository : IPrecoCategoriaRepository {
    private readonly DatabaseContext _context;

    public PrecoCategoriaRepository(DatabaseContext context) {
        _context = context;
    }

    public async Task<List<CategoriaPreco>> GetAllAsync() {
        return await _context.CategoriaPrecos.Include(p => p.Categoria).ToListAsync();
    }

    public async Task<CategoriaPreco?> GetByIdAsync(int id) {
        return await _context.CategoriaPrecos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(CategoriaPreco precoCategoria) {
        _context.CategoriaPrecos.Add(precoCategoria);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(CategoriaPreco precoCategoria) {
        _context.Entry(precoCategoria).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id) {
        var precoCategoria = await _context.CategoriaPrecos.FindAsync(id);
        if (precoCategoria != null) {
            _context.CategoriaPrecos.Remove(precoCategoria);
            await _context.SaveChangesAsync();
        }
    }
}