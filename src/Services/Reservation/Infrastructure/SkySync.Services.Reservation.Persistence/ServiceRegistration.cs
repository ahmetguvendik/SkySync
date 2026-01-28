using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.SagaStateMachine.StateDbContexts;
using SkySync.SagaStateMachine.StateInstances;
using SkySync.SagaStateMachine.StateMachines;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Application.UnitOfWorks;
using SkySync.Services.Reservation.Persistence.Contexts;
using SkySync.Services.Reservation.Persistence.Repositories;
using SkySync.Services.Reservation.Persistence.UnitOfWorks;
using SkySync.Shared;
using SkySync.Shared.Commands;

namespace SkySync.Services.Reservation.Persistence;

public static class ServiceRegistration
{
    public static void AddPersistenceService(this IServiceCollection collection, IConfiguration configuration)
    {
        // Worker için ReservationConnection, Reservation Service için DefaultConnection kullan
        var connectionString = configuration.GetConnectionString("ReservationConnection") 
                               ?? configuration.GetConnectionString("DefaultConnection");
        collection.AddDbContext<ReservationServiceDbContext>(opt =>
            opt.UseNpgsql(connectionString));
        
        // Repositories
        collection.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        collection.AddScoped<IOutboxRepository, OutboxRepository>();
        collection.AddScoped<IUnitOfWork, UnitOfWork>();
    }

    /// <summary>
    /// MassTransit ve RabbitMQ Service Registration
    /// Senior Level: Service Registration Pattern - Infrastructure concerns
    /// </summary>
    public static void AddMassTransitService(this IServiceCollection services, IConfiguration configuration)
    {
        // CRITICAL: ReservationStateDbContext'i DI container'a ekle
        // Saga State Machine'in state'leri kaydetmesi için gerekli
        var sagaConnectionString = configuration.GetConnectionString("SagaConnection") 
            ?? configuration.GetConnectionString("DefaultConnection");
        
        services.AddDbContext<ReservationStateDbContext>(options =>
        {
            options.UseNpgsql(sagaConnectionString);
        });

        services.AddMassTransit(x =>
        {
            // Saga State Machine - Yeni SagaStateMachine projesinden
            x.AddSagaStateMachine<ReservationStateMachine, ReservationState>()
                .EntityFrameworkRepository(r =>
                {
                    // PostgreSQL için Optimistic locking kullan (Pessimistic SQL Server syntax'ı kullanır)
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    
                    // PostgreSQL için pessimistic locking gerekirse:
                    // r.LockStatementProvider = new PostgresLockStatementProvider();
                    
                    r.ExistingDbContext<ReservationStateDbContext>();
                });

            x.UsingRabbitMq((context, cfg) =>
            {
                // CRITICAL: .NET 9 uyumluluğu için Newtonsoft JSON serializer kullan
                cfg.UseNewtonsoftJsonSerializer();
                
                // CloudAMQP connection string kullan (sadece CloudAMQP, local fallback yok)
                var connectionString = configuration["RabbitMQ:ConnectionString"];
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        "RabbitMQ:ConnectionString configuration is required. Please provide CloudAMQP connection string.");
                }

                // CloudAMQP connection string formatı: amqps://username:password@host/vhost
                cfg.Host(connectionString);

                // Endpoint Mapping - Senior Level: Direct routing for commands
                EndpointConvention.Map<ReserveSeatCommand>(new Uri($"queue:{RabbitMqSettings.FlightReserveSeatQueue}"));
                EndpointConvention.Map<ProcessPaymentCommand>(new Uri($"queue:{RabbitMqSettings.PaymentProcessQueue}"));
                EndpointConvention.Map<ReleaseSeatCommand>(new Uri($"queue:{RabbitMqSettings.FlightReleaseSeatQueue}"));

                cfg.ReceiveEndpoint(RabbitMqSettings.ReservationSagaQueue, e =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            });
        });
    }
}
