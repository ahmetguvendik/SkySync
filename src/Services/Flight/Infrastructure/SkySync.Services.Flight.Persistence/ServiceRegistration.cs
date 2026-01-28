using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Shared;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Services.Flight.Persistence.Consumers;
using SkySync.Services.Flight.Persistence.Contexts;
using SkySync.Services.Flight.Persistence.Repositories;
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
        collection.AddScoped<IOutboxRepository, OutboxRepository>();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
        
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

                cfg.ReceiveEndpoint(RabbitMqSettings.FlightReserveSeatQueue, e =>
                {
                    e.ConfigureConsumer<ReserveSeatCommandConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.FlightReleaseSeatQueue, e =>
                {
                    e.ConfigureConsumer<ReleaseSeatCommandConsumer>(context);
                });
            });
        });
    }
}