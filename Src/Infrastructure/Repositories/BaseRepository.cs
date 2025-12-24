namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IBaseRepository {
    Task SaveChangesAsync();
}
public class BaseRepository(DatabaseContext dbContext) : IBaseRepository {
    protected readonly DatabaseContext _dbContext = dbContext;

    public async Task SaveChangesAsync() {
        await _dbContext.SaveChangesAsync();
    }
}