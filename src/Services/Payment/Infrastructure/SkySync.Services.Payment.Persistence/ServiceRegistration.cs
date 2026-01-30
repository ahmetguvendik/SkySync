using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Services.Payment.Persistence.Contexts;
using SkySync.Services.Payment.Persistence.Consumers;
using SkySync.Services.Payment.Persistence.Services;
using SkySync.Shared;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Payment.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - PostgreSQL (Inbox Pattern için)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<PaymentServiceDbContext>(opt => opt.UseNpgsql(connectionString));
        
        // Inbox Service - Duplicate payment prevention (CRITICAL!)
        services.AddScoped<IInboxService, InboxService>();
    }

    public static void AddMassTransitService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<ProcessPaymentConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = configuration["RabbitMQ:ConnectionString"];
                cfg.Host(connectionString);

                cfg.ReceiveEndpoint(RabbitMqSettings.PaymentProcessQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<ProcessPaymentConsumer>(context);
                });
            });
        });
    }
}
