using Microsoft.EntityFrameworkCore.Storage;
using SkySync.Services.Identity.Application.UnitOfWorks;
using SkySync.Services.Identity.Persistence.Contexts;

namespace SkySync.Services.Identity.Persistence.UnitOfWorks;

public class UnitOfWork : IUnitOfWork
{
    private readonly IdentityServiceDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(IdentityServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _dbContext.Dispose();
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            return;
        }

        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }
}
