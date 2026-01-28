using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Domain.Entities;
using SkySync.Services.Reservation.Persistence.Contexts;

namespace SkySync.Services.Reservation.Persistence.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    private readonly ReservationServiceDbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(ReservationServiceDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(x => !x.IsDeleted).Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var guidId))
            return null;

        return await _dbSet.FirstOrDefaultAsync(x => x.Id == guidId && !x.IsDeleted, cancellationToken);
    }

    public async Task CreateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedTime = DateTime.UtcNow;
        entity.ModifiedTime = DateTime.UtcNow;
        entity.IsDeleted = false;
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.ModifiedTime = DateTime.UtcNow;
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.IsDeleted = true;
        entity.ModifiedTime = DateTime.UtcNow;
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public IQueryable<T> GetQueryable()
    {
        return _dbSet.Where(x => !x.IsDeleted).AsQueryable();
    }
}
