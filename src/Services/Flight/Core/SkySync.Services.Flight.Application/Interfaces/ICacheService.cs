namespace SkySync.Services.Flight.Application.Interfaces;

/// <summary>
/// Cache service interface - Redis için abstraction
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Cache'den değer getirir
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Cache'e değer set eder
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Cache'den değer siler
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache'den pattern'e göre değerleri siler (örn: "flights:*")
    /// </summary>
    Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Distributed lock alır (Cache Stampede önleme için)
    /// </summary>
    Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lock'u serbest bırakır
    /// </summary>
    Task ReleaseLockAsync(IDistributedLock distributedLock, CancellationToken cancellationToken = default);
}

/// <summary>
/// Distributed lock interface
/// </summary>
public interface IDistributedLock : IDisposable
{
    bool IsAcquired { get; }
}
