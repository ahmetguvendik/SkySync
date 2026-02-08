using MassTransit;
using SkySync.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Services.Payment.Persistence.Contexts;
using SkySync.Services.Payment.Persistence.Consumers;
using SkySync.Services.Payment.Application.Interfaces;
using SkySync.Services.Payment.Application.UnitOfWorks;
using SkySync.Services.Payment.Persistence.Repositories;
using SkySync.Services.Payment.Persistence.UnitOfWorks;
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

        // Generic Repository & Unit of Work
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Event Publisher
        services.AddScoped<IEventPublisher, EventPublisher>();
    }

    public static void AddMassTransitService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            // ProcessPaymentConsumer kaldırıldı: Ödeme frontend'den POST /api/v1/payment/process ile tetiklenir
            x.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = configuration["RabbitMQ:ConnectionString"];
                cfg.Host(connectionString);
                // Saga (Reservation) ile aynı serializer - PaymentCompletedEvent / PaymentFailedEvent uyumluluğu
                cfg.UseNewtonsoftJsonSerializer();
            });
        });
    }
}
