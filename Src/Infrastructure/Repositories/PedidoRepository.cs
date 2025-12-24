using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Application.DTOs;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IPedidoRepository : IBaseRepository {
    Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro);

    Task<Pedido?> GetByIdAsync(int id);
    Task SaveAsync(Pedido pedido, bool commit = true);
    Task<bool> DeleteAsync(int id);
}

public class PedidoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IPedidoRepository {

    public async Task<List<Pedido>> GetAllAsync(FiltroPedidoDTO filtro) {
        var query = _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)!
                .ThenInclude(j => j.Jogo)
            .AsNoTracking()
            .AsQueryable();

        if (filtro.IdCliente.HasValue) {
            query = query.Where(p => p.IdCliente == filtro.IdCliente.Value);
        }

        if (filtro.DataInicial.HasValue) {
            query = query.Where(p => p.DataHora >= filtro.DataInicial.Value);
        }

        if (filtro.DataFinal.HasValue) {
            query = query.Where(p => p.DataHora <= filtro.DataFinal.Value);
        }

        if (filtro.Status.HasValue) {
            query = query.Where(p => p.Status == filtro.Status.Value);
        }

        if (filtro.Atrasados) {
            // nao vou considerar horas, apenas dias
            query = query.Where(p => p.Items!.Any(i => i.DataDevolucao.Date < DateTime.Today && i.Jogo.Status == StatusJogo.Alugado));
        }

        return await query.ToListAsync();
    }

    public async Task<Pedido?> GetByIdAsync(int id) {
        return await _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)!
            .ThenInclude(j => j.Jogo)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task SaveAsync(Pedido pedido, bool commit = true) {
        if (pedido.Id == 0) {
            _dbContext.Pedidos.Add(pedido);
        } else if (_dbContext.Entry(pedido).State == EntityState.Detached) {
            _dbContext.Pedidos.Update(pedido);
        }
        if (!commit)
            return;

        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id) {
        return await _dbContext.Pedidos
            .Where(p => p.Id == id)
            .ExecuteDeleteAsync() > 0;
    }


}
