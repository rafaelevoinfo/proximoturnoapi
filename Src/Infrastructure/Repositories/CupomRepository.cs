using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs.Filtros;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface ICupomRepository : IBaseRepository
{
    Task<Cupom?> GetByIdAsync(int id);
    Task<Cupom?> GetByCodigoAsync(string codigo);
    Task<List<Cupom>> GetAllAsync(FiltroCupomDTO filtro);
    Task<int> GetUsoCountGlobalAsync(int cupomId, int? idPedidoExcluir = null);
    Task<int> GetUsoCountClienteAsync(int cupomId, int clienteId, int? idPedidoExcluir = null);
    Task SaveAsync(Cupom cupom, bool commit = true);
    Task<bool> DeleteAsync(int id);
    Task<bool> IsUsedInPedidoAsync(int id);
}

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

    public async Task<int> GetUsoCountGlobalAsync(int cupomId, int? idPedidoExcluir = null)
    {
        var query = _dbContext.Pedidos.AsNoTracking().Where(p => p.IdCupom == cupomId && p.Status != StatusPedido.Cancelado);
        if (idPedidoExcluir.HasValue)
        {
            query = query.Where(p => p.Id != idPedidoExcluir.Value);
        }
        return await query.CountAsync();
    }

    public async Task<int> GetUsoCountClienteAsync(int cupomId, int clienteId, int? idPedidoExcluir = null)
    {
        var query = _dbContext.Pedidos.AsNoTracking().Where(p => p.IdCupom == cupomId && p.Cliente.Id == clienteId && p.Status != StatusPedido.Cancelado);
        if (idPedidoExcluir.HasValue)
        {
            query = query.Where(p => p.Id != idPedidoExcluir.Value);
        }
        return await query.CountAsync();
    }

    public async Task SaveAsync(Cupom cupom, bool commit = true)
    {
        await SaveChangesAsync(_dbContext.Cupons, cupom, commit);
    }

    public async Task<bool> IsUsedInPedidoAsync(int id)
    {
        return await _dbContext.Pedidos.AnyAsync(p => p.IdCupom == id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _dbContext.Cupons
            .Where(c => c.Id == id)
            .ExecuteDeleteAsync() > 0;
    }
}
