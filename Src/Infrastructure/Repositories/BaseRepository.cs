using Microsoft.EntityFrameworkCore.Storage;

namespace ProximoTurnoApi.Infrastructure.Repositories;

public interface IBaseRepository {
    Task SaveChangesAsync();
    Task StartTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
public class BaseRepository(DatabaseContext dbContext) : IBaseRepository {
    protected readonly DatabaseContext _dbContext = dbContext;
    private IDbContextTransaction? _currentTransaction;

    public async Task SaveChangesAsync() {
        await _dbContext.SaveChangesAsync();
    }

    public async Task StartTransactionAsync() {
        if (_currentTransaction is not null) {
            return;
        }
        _currentTransaction = await _dbContext.Database.BeginTransactionAsync();
    }
    public async Task CommitTransactionAsync() {
        if (_currentTransaction is not null) {
            await _currentTransaction.CommitAsync();
        }
    }

    public async Task RollbackTransactionAsync() {
        if (_currentTransaction is not null) {
            await _currentTransaction.RollbackAsync();
        }
    }
}