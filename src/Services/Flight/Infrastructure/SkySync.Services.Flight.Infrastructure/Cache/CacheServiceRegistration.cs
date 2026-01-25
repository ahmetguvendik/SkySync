using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using SkySync.Services.Flight.Application.Interfaces;

namespace SkySync.Services.Flight.Infrastructure.Cache;

public static class CacheServiceRegistration
{
    public static void AddCacheService(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis") 
                                    ?? configuration["Redis:ConnectionString"] 
                                    ?? "localhost:6379";

        // Redis Connection Multiplexer (Distributed Lock için)
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            return ConnectionMultiplexer.Connect(redisConnectionString);
        });

        // Redis Distributed Cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "SkySync:";
        });

        // Cache Service
        services.AddScoped<ICacheService, RedisCacheService>();
    }
}
