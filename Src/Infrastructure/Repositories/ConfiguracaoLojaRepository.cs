using Microsoft.EntityFrameworkCore;
using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IConfiguracaoLojaRepository {
    /// <summary>A linha de configuração, ou null enquanto o admin nunca salvou nada.</summary>
    Task<ConfiguracaoLoja?> GetAsync();
    Task SalvarAsync(ConfiguracaoLoja configuracao);
}

public class ConfiguracaoLojaRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IConfiguracaoLojaRepository {

    public Task<ConfiguracaoLoja?> GetAsync() {
        return _dbContext.ConfiguracoesLoja.AsTracking().OrderBy(c => c.Id).FirstOrDefaultAsync();
    }

    public Task SalvarAsync(ConfiguracaoLoja configuracao) => SaveChangesAsync(_dbContext.ConfiguracoesLoja, configuracao);
}
