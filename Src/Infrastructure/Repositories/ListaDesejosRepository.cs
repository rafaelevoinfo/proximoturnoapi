using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IListaDesejosRepository : IBaseRepository {
    Task<List<ItemListaDesejos>> GetByClienteAsync(int idCliente);
    Task<bool> IsInWishlistAsync(int idCliente, int idJogo);
    Task AddAsync(ItemListaDesejos item);
    Task RemoveAsync(int idCliente, int idJogo);
}

public class ListaDesejosRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IListaDesejosRepository {
    public async Task<List<ItemListaDesejos>> GetByClienteAsync(int idCliente) =>
        await _dbContext.ItensListaDesejos
            .Include(i => i.Jogo)
                .ThenInclude(j => j!.Tags)
            .Include(i => i.Jogo)
                .ThenInclude(j => j!.Links)
            .Include(i => i.Jogo)
                .ThenInclude(j => j!.Fotos)
            .Include(i => i.Jogo)
                .ThenInclude(j => j!.Copias)
            .Where(i => i.IdCliente == idCliente)
            .ToListAsync();

    public async Task<bool> IsInWishlistAsync(int idCliente, int idJogo) =>
        await _dbContext.ItensListaDesejos.AnyAsync(i => i.IdCliente == idCliente && i.IdJogo == idJogo);

    public async Task AddAsync(ItemListaDesejos item) {
        _dbContext.ItensListaDesejos.Add(item);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAsync(int idCliente, int idJogo) {
        await _dbContext.ItensListaDesejos
            .Where(i => i.IdCliente == idCliente && i.IdJogo == idJogo)
            .ExecuteDeleteAsync();
    }
}
