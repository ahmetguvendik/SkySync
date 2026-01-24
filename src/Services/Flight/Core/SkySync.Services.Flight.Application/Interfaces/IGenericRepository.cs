using System.Linq.Expressions;

namespace SkySync.Services.Flight.Application.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<T>> GetAllAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task CreateAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    IQueryable<T> GetQueryable();
}