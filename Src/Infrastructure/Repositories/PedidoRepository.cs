using Microsoft.EntityFrameworkCore;

using ProximoTurnoApi.Infrastructure.Repositories;
using ProximoTurnoApi.Infrastructure.Models;

public interface IPedidoRepository {
    Task<List<Pedido>> GetAllAsync();
    Task<Pedido?> GetByIdAsync(int id);
    Task SaveAsync(Pedido pedido, bool commit = true);
    Task<bool> DeleteAsync(int id);
    Task SaveChangesAsync();

}

public class PedidoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IPedidoRepository {

    public async Task<List<Pedido>> GetAllAsync() {
        return await _dbContext.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Items)!
            .ThenInclude(j => j.Jogo)
            .ToListAsync();
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
        } else {
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
