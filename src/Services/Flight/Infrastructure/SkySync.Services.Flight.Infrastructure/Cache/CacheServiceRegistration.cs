using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SkySync.Services.Flight.Application.Interfaces;

namespace SkySync.Services.Flight.Infrastructure.Cache;

public static class CacheServiceRegistration
{
    /// <summary>
    /// Redis cache servisini ekler. multiplexer verilirse aynı bağlantı kullanılır (OpenTelemetry Redis instrumentation için gerekli).
    /// </summary>
    public static void AddCacheService(
        this IServiceCollection services,
        IConfiguration configuration,
        IConnectionMultiplexer? multiplexer = null)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis")
                                    ?? configuration["Redis:ConnectionString"]
                                    ?? "localhost:6379";

        var redis = multiplexer ?? ConnectionMultiplexer.Connect(redisConnectionString);

        services.AddSingleton<IConnectionMultiplexer>(redis);

        services.AddStackExchangeRedisCache(options =>
        {
            options.ConnectionMultiplexerFactory = () => Task.FromResult(redis);
            options.InstanceName = "SkySync:";
        });

        services.AddScoped<ICacheService, RedisCacheService>();
    }
}
