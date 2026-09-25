using ProximoTurnoApi.Infrastructure.Models;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IUsoLlmRepository : IBaseRepository {
    Task RegistrarAsync(UsoLlm uso);

    /// <summary>Caminho síncrono, para o override sync da policy do pipeline.</summary>
    void Registrar(UsoLlm uso);
}

public class UsoLlmRepository(DatabaseContext dbContext) : BaseRepository(dbContext), IUsoLlmRepository {

    public Task RegistrarAsync(UsoLlm uso) => SaveChangesAsync(_dbContext.UsosLlm, uso);

    // Existe porque PipelinePolicy exige o par sync/async, e virar um no outro com
    // GetAwaiter().GetResult() dentro de um pipeline de I/O e pedir deadlock.
    public void Registrar(UsoLlm uso) {
        _dbContext.UsosLlm.Add(uso);
        _dbContext.SaveChanges();
    }
}
