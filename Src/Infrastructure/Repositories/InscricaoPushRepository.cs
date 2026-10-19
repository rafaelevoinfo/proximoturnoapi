using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IInscricaoPushRepository {
    Task<InscricaoPush?> GetByEndpointAsync(string endpoint);
    Task SalvarAsync(InscricaoPush inscricao);
    Task<bool> RemoverAsync(string endpoint, string idUsuario);
    Task RemoverPorIdAsync(int id);

    /// <summary>Inscrições de quem tem o papel Admin hoje: perder o papel corta as notificações.</summary>
    Task<List<InscricaoPush>> GetDosAdminsAsync();

    /// <summary>Inscrições do login com este e-mail: é pelo e-mail que o cliente chega ao usuário.</summary>
    Task<List<InscricaoPush>> GetDoUsuarioPorEmailAsync(string email);
}

public class InscricaoPushRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IInscricaoPushRepository {

    public Task<InscricaoPush?> GetByEndpointAsync(string endpoint) {
        return _dbContext.InscricoesPush.AsTracking().FirstOrDefaultAsync(i => i.Endpoint == endpoint);
    }

    public Task SalvarAsync(InscricaoPush inscricao) => SaveChangesAsync(_dbContext.InscricoesPush, inscricao);

    public async Task<bool> RemoverAsync(string endpoint, string idUsuario) {
        return await _dbContext.InscricoesPush
            .Where(i => i.Endpoint == endpoint && i.IdUsuario == idUsuario)
            .ExecuteDeleteAsync() > 0;
    }

    public async Task RemoverPorIdAsync(int id) {
        await _dbContext.InscricoesPush.Where(i => i.Id == id).ExecuteDeleteAsync();
    }

    public async Task<List<InscricaoPush>> GetDosAdminsAsync() {
        var idsAdmins = from ur in _dbContext.Set<IdentityUserRole<string>>()
                        join r in _dbContext.Roles on ur.RoleId equals r.Id
                        where r.Name == Roles.Admin
                        select ur.UserId;

        return await _dbContext.InscricoesPush
            .Where(i => idsAdmins.Contains(i.IdUsuario))
            .ToListAsync();
    }

    public async Task<List<InscricaoPush>> GetDoUsuarioPorEmailAsync(string email) {
        // O Identity normaliza com ToUpperInvariant, e NormalizedEmail é a coluna indexada.
        var emailNormalizado = email.Trim().ToUpperInvariant();
        var idsUsuario = _dbContext.Users
            .Where(u => u.NormalizedEmail == emailNormalizado)
            .Select(u => u.Id);

        return await _dbContext.InscricoesPush
            .Where(i => idsUsuario.Contains(i.IdUsuario))
            .ToListAsync();
    }
}
