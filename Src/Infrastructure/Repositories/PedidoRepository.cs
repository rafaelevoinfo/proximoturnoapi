using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Domain;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IPedidoRepository : IBaseRepository {
    Task SaveAsync(Pedido pedido, bool commit = true);
    Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro);
    Task<Pedido?> GetByIdAsync(int id);
    Task<bool> DeleteAsync(int id);
}

public class PedidoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IPedidoRepository {

    public async Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro) {
        var query = _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)!
                .ThenInclude(j => j.JogoCopia)
                    .ThenInclude(jc => jc.Jogo)
            .AsQueryable();

        if (filtro.IdCliente.HasValue) {
            query = query.Where(p => p.Cliente.Id == filtro.IdCliente.Value);
        }

        if (filtro.DataInicial.HasValue) {
            var dataInicial = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(p => p.DataHora >= dataInicial);
        }

        if (filtro.DataFinal.HasValue) {
            var dataFinal = filtro.DataFinal.Value.ToDateTime(new TimeOnly(23, 59, 59));
            query = query.Where(p => p.DataHora <= dataFinal);
        }

        if (filtro.Status.HasValue) {
            query = query.Where(p => p.Status == filtro.Status.Value);
        }

        if (filtro.Atrasados) {
            // nao vou considerar horas, apenas dias
            query = query.Where(p => p.Items!.Any(i => i.DataDevolucao.Date < DateTime.Today && i.JogoCopia.Status == StatusJogo.Alugado));
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(p => p.Id)
            .ToListAsync();
    }

    public async Task<Pedido?> GetByIdAsync(int id) {
        return await _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)!
                .ThenInclude(j => j.JogoCopia)
                    .ThenInclude(jc => jc.Jogo)
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Pedidos
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync() > 0;
    }

    public async Task SaveAsync(Pedido pedido, bool commit) {
        await SaveChangesAsync(_dbContext.Pedidos, pedido, commit);
    }
}
