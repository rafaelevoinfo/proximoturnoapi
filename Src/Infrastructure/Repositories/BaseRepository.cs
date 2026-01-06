using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using ProximoTurnoApi.Infrastructure.Models;

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

    protected async Task SaveChangesAsync<T>(DbSet<T> dbSet, T entity, bool commit = true) where T : BaseModel {
        if (entity.Id == 0) {
            dbSet.Add(entity);
        } else if (_dbContext.Entry(entity).State == EntityState.Detached) {
            dbSet.Update(entity);
        }
        if (!commit)
            return;

        await _dbContext.SaveChangesAsync();
    }

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