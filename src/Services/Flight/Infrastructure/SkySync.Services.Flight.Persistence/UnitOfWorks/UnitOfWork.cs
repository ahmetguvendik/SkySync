using Microsoft.EntityFrameworkCore.Storage;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Services.Flight.Persistence.Contexts;

namespace SkySync.Services.Flight.Persistence.UnitOfWorks;

public class UnitOfWork : IUnitOfWork
{
    private readonly FlightServiceDbContext _dbContext;
    private IDbContextTransaction? _dbContextTransaction;

    public UnitOfWork(FlightServiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Dispose()
    {
        _dbContextTransaction?.Dispose();
        _dbContext.Dispose();
        //garbega collector a temizleme izni verir.
        //o an silmez silinebilir yapar sadece
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContextTransaction != null)
        {
            return; // Transaction already started
        }
        _dbContextTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContextTransaction == null)
        {
            return;
        }
        await _dbContextTransaction.CommitAsync(cancellationToken);
        await _dbContextTransaction.DisposeAsync();
        _dbContextTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContextTransaction == null)
        {
            return;
        }
        await _dbContextTransaction.RollbackAsync(cancellationToken);
        await _dbContextTransaction.DisposeAsync();
        _dbContextTransaction = null;
    }
}