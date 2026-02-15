using MassTransit;
using SkySync.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Shared;
using SkySync.Shared.InboxPattern;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Services.Flight.Persistence.Consumers;
using SkySync.Services.Flight.Persistence.Contexts;
using SkySync.Services.Flight.Persistence.Repositories;
using SkySync.Services.Flight.Persistence.Services;
using SkySync.Services.Flight.Persistence.UnitOfWorks;

namespace SkySync.Services.Flight.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceService(this IServiceCollection collection, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        collection.AddDbContext<FlightServiceDbContext>(opt =>
            opt.UseNpgsql(connectionString));

        //Repositories
        collection.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        collection.AddScoped<IAircraftRepository, AircraftRepository>();
        collection.AddScoped<IOutboxRepository, OutboxRepository>();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();

        // Inbox Service - Idempotency for seat operations (critical!)
        collection.AddScoped<IInboxService, InboxService>();
    }

    public static void AddMassTransitService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<ReserveSeatCommandConsumer>();
            x.AddConsumer<ReleaseSeatCommandConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var connectionString = configuration["RabbitMQ:ConnectionString"];
                cfg.Host(connectionString);
                cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);

                cfg.ReceiveEndpoint(RabbitMqSettings.FlightReserveSeatQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<ReserveSeatCommandConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.FlightReleaseSeatQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<ReleaseSeatCommandConsumer>(context);
                });
            });
        });
    }
}