using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IContratoRepository : IBaseRepository {
    Task SaveAsync(ContratoAutentique contrato, bool commit = true);
    Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido);
    Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId);
    Task<List<ContratoAutentique>> GetActiveByPedidoIdsAsync(List<int> idPedidos);
    Task InativarContratosPorPedidoIdAsync(int idPedido);
}

public class ContratoRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IContratoRepository {

    public async Task SaveAsync(ContratoAutentique contrato, bool commit) {
        await SaveChangesAsync(_dbContext.ContratosAutentique, contrato, commit);
    }

    public async Task<ContratoAutentique?> GetByPedidoIdAsync(int idPedido) {
        return await _dbContext.ContratosAutentique
            .AsTracking()
            .FirstOrDefaultAsync(c => c.IdPedido == idPedido && c.Ativo);
    }

    public async Task<ContratoAutentique?> GetByAutentiqueDocumentIdAsync(string autentiqueDocumentId) {
        return await _dbContext.ContratosAutentique
            .AsTracking()
            .FirstOrDefaultAsync(c => c.AutentiqueDocumentId == autentiqueDocumentId);
    }

    public async Task<List<ContratoAutentique>> GetActiveByPedidoIdsAsync(List<int> idPedidos) {
        if (idPedidos == null || idPedidos.Count == 0) {
            return [];
        }
        return await _dbContext.ContratosAutentique
            .AsNoTracking()
            .Where(c => idPedidos.Contains(c.IdPedido) && c.Ativo)
            .ToListAsync();
    }

    public async Task InativarContratosPorPedidoIdAsync(int idPedido) {
        await _dbContext.ContratosAutentique
            .Where(c => c.IdPedido == idPedido && c.Ativo)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ativo, false));
    }
}
