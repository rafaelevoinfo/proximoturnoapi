using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IContratoRepository : IBaseRepository {
    Task SaveAsync(ContratoAutentique contrato, bool commit = true);
    Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido);
    Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId);
}

public class ContratoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IContratoRepository {

    public async Task SaveAsync(ContratoAutentique contrato, bool commit) {
        await SaveChangesAsync(_dbContext.ContratosAutentique, contrato, commit);
    }

    public async Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) {
        return await _dbContext.ContratosAutentique
            .Include(c => c.Pedido)
            .AsTracking()
            .FirstOrDefaultAsync(c => c.IdPedido == idPedido);
    }

    public async Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId) {
        return await _dbContext.ContratosAutentique
            .AsTracking()
            .FirstOrDefaultAsync(c => c.AutentiqueDocumentId == autentiqueDocumentId);
    }
}
