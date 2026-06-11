using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public class CupomRepository(DatabaseContext dbContext) : BaseRepository(dbContext), ICupomRepository
{
    public async Task<Cupom?> GetByIdAsync(int id)
    {
        return await _dbContext.Cupons
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cupom?> GetByCodigoAsync(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }
        return await _dbContext.Cupons
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Codigo == codigo);
    }

    public async Task<List<Cupom>> GetAllAsync(FiltroCupomDTO filtro)
    {
        var query = _dbContext.Cupons.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            query = query.Where(c => c.Codigo.Contains(filtro.Search));
        }

        if (filtro.ApenasAtivos.HasValue && filtro.ApenasAtivos.Value)
        {
            query = query.Where(c => c.Ativo);
        }

        return await query
            .OrderBy(c => c.Codigo)
            .ToListAsync();
    }

    public async Task<int> GetUsoCountGlobalAsync(int cupomId)
    {
        return await _dbContext.Pedidos
            .CountAsync(p => p.IdCupom == cupomId && p.Status != StatusPedido.Cancelado);
    }

    public async Task<int> GetUsoCountClienteAsync(int cupomId, int clienteId)
    {
        return await _dbContext.Pedidos
            .CountAsync(p => p.IdCupom == cupomId && p.Cliente.Id == clienteId && p.Status != StatusPedido.Cancelado);
    }

    public async Task SaveAsync(Cupom cupom, bool commit = true)
    {
        await SaveChangesAsync(_dbContext.Cupons, cupom, commit);
    }
}
