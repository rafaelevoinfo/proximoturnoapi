using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Data;
using ProximoTurnoApi.Models;
using System.Collections.Generic;
using System.Linq;

namespace ProximoTurnoApi.Repositories;

public interface IJogoRepository {
    Task<List<Jogo>> GetAll();
    Task<Jogo?> GetById(int id);
    Task<Jogo> Add(Jogo jogo);
    Task Update(Jogo jogo);
    Task Delete(int id);
}

public class JogoRepository : IJogoRepository {
    private readonly ProximoTurnoDbContext _context;

    public JogoRepository(ProximoTurnoDbContext context) {
        _context = context;
    }

    public async Task<List<Jogo>> GetAll() {
        return await _context.Jogos.ToListAsync();
    }

    public async Task<Jogo?> GetById(int id) {
        return await _context.Jogos.FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<Jogo> Add(Jogo jogo) {
        _context.Jogos.Add(jogo);
        await _context.SaveChangesAsync();
        return jogo;
    }

    public async Task Update(Jogo jogo) {
        _context.Jogos.Update(jogo);
        await _context.SaveChangesAsync();
    }

    public async Task Delete(int id) {
        var jogo = await GetById(id);
        if (jogo != null) {
            _context.Jogos.Remove(jogo);
            await _context.SaveChangesAsync();
        }
    }
}