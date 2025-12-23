namespace ProximoTurnoApi.Infrastructure.Repositories;

public class BaseRepository(DatabaseContext dbContext) {
    protected readonly DatabaseContext _dbContext = dbContext;

    public async Task SaveChangesAsync() {
        await _dbContext.SaveChangesAsync();
    }
}