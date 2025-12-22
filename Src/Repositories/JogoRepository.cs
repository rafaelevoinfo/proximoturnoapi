using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Models;

namespace ProximoTurnoApi.Repositories;

public interface IJogoRepository {
    Task<List<Jogo>> GetAllAsync();
    Task<Jogo?> GetByIdAsync(int id);
    Task AddAsync(Jogo jogo);
    Task UpdateAsync(Jogo jogo);
    Task DeleteAsync(int id);
}

public class JogoRepository : IJogoRepository {
    private readonly DatabaseContext _context;

    public JogoRepository(DatabaseContext context) {
        _context = context;
    }

    public async Task<List<Jogo>> GetAllAsync() {
        return await _context.Jogos.Include(j => j.Categoria).ToListAsync();
    }

    public async Task<Jogo?> GetByIdAsync(int id) {
        return await _context.Jogos.Include(j => j.Categoria).FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task AddAsync(Jogo jogo) {
        _context.Jogos.Add(jogo);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Jogo jogo) {
        _context.Entry(jogo).State = EntityState.Modified;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id) {
        var jogo = await GetByIdAsync(id);
        if (jogo != null) {
            _context.Jogos.Remove(jogo);
            await _context.SaveChangesAsync();
        }
    }
}
