using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SkySync.Services.Flight.Application.Interfaces;

namespace SkySync.Services.Flight.Infrastructure.Cache;

/// <summary>
/// Redis Cache Service Implementation - Cache Aside Pattern için
/// Senior Level: Distributed Locking, Fail-Safe
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private const int DefaultExpirationMinutes = 30;
    private const int LockTimeoutSeconds = 10; // Lock için maksimum bekleme süresi

    public RedisCacheService(
        IDistributedCache distributedCache,
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache;
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var cachedValue = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(cachedValue))
            {
                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }

            _logger.LogDebug("Cache hit for key: {Key}", key);
            return JsonSerializer.Deserialize<T>(cachedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
            // Cache hatası durumunda null döndür, DB'den alsın (Fail-Safe)
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(DefaultExpirationMinutes)
            };

            await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);
            _logger.LogDebug("Value cached for key: {Key}, Expiration: {Expiration}", key, options.AbsoluteExpirationRelativeToNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting value to cache for key: {Key}", key);
            // Cache hatası durumunda exception fırlatma, sadece logla (Fail-Safe)
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
            _logger.LogDebug("Cache removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing value from cache for key: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            _logger.LogWarning("RemoveByPatternAsync called with empty pattern.");
            return;
        }

        try
        {
            var database = _redis.GetDatabase();
            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                if (!server.IsConnected || server.IsReplica)
                    continue;

                foreach (var key in server.Keys(pattern: pattern))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await database.KeyDeleteAsync(key);
                    _logger.LogDebug("Cache key removed via pattern. Pattern={Pattern}, Key={Key}", pattern, key);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache keys with pattern: {Pattern}", pattern);
        }
    }

    public async Task<IDistributedLock?> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockKey = $"lock:{key}";
            var lockValue = Guid.NewGuid().ToString();
            var database = _redis.GetDatabase();

            // SET NX EX - Atomic lock acquisition
            var acquired = await database.StringSetAsync(
                lockKey,
                lockValue,
                expiry,
                When.NotExists,
                CommandFlags.None);

            if (acquired)
            {
                _logger.LogDebug("Lock acquired for key: {Key}", key);
                return new RedisDistributedLock(database, lockKey, lockValue, _logger);
            }

            _logger.LogDebug("Lock not acquired for key: {Key} (already locked)", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock for key: {Key}", key);
            return null;
        }
    }

    public async Task ReleaseLockAsync(IDistributedLock distributedLock, CancellationToken cancellationToken = default)
    {
        try
        {
            distributedLock.Dispose();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock");
        }
    }
}

/// <summary>
/// Redis Distributed Lock Implementation
/// </summary>
internal class RedisDistributedLock : IDistributedLock
{
    private readonly IDatabase _database;
    private readonly string _lockKey;
    private readonly string _lockValue;
    private readonly ILogger _logger;
    private bool _disposed;

    public bool IsAcquired { get; private set; }

    public RedisDistributedLock(IDatabase database, string lockKey, string lockValue, ILogger logger)
    {
        _database = database;
        _lockKey = lockKey;
        _lockValue = lockValue;
        _logger = logger;
        IsAcquired = true;
    }

    public void Dispose()
    {
        if (_disposed || !IsAcquired)
            return;

        try
        {
            // Lua script ile atomic release (sadece kendi lock'umuzu silelim)
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            _database.ScriptEvaluate(script, new RedisKey[] { _lockKey }, new RedisValue[] { _lockValue });
            IsAcquired = false;
            _logger.LogDebug("Lock released for key: {Key}", _lockKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock for key: {Key}", _lockKey);
        }
        finally
        {
            _disposed = true;
        }
    }
}
