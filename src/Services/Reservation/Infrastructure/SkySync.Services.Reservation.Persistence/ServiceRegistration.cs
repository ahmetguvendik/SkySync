using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SkySync.Services.Reservation.Persistence.Consumers;
using SkySync.SagaStateMachine.StateDbContexts;
using SkySync.SagaStateMachine.StateInstances;
using SkySync.SagaStateMachine.StateMachines;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Persistence.Services;
using SkySync.Services.Reservation.Application.UnitOfWorks;
using SkySync.Services.Reservation.Persistence.Contexts;
using SkySync.Services.Reservation.Persistence.Repositories;
using SkySync.Services.Reservation.Persistence.UnitOfWorks;
using SkySync.Shared;
using SkySync.Shared.Commands;
using SkySync.Shared.InboxPattern;

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

        // Read model – yolcu rezervasyonları + FlightNumber (FlightSummary join)
        collection.AddScoped<IPassengerReservationsRepository, PassengerReservationsRepository>();

        // Inbox — status consumer idempotency
        collection.AddScoped<IInboxService, InboxService>();
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

        services.AddQuartz();

        services.AddMassTransit(x =>
        {
            x.AddQuartzConsumers();

            // Saga State Machine
            x.AddSagaStateMachine<ReservationStateMachine, ReservationState>()
                .EntityFrameworkRepository(r =>
                {
                    // PostgreSQL için Optimistic locking kullan (Pessimistic SQL Server syntax'ı kullanır)
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    
                    // PostgreSQL için pessimistic locking gerekirse:
                    // r.LockStatementProvider = new PostgresLockStatementProvider();
                    
                    r.ExistingDbContext<ReservationStateDbContext>();
                });

            // Reservation status update consumers
            x.AddConsumer<ReservationConfirmedStatusConsumer>();
            x.AddConsumer<ReservationSeatFailedStatusConsumer>();
            x.AddConsumer<ReservationPaymentFailedStatusConsumer>();
            x.AddConsumer<ReservationTimedOutStatusConsumer>();
            // Flight read model – FlightCreatedEvent ile FlightSummary güncellenir
            x.AddConsumer<FlightCreatedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {

                cfg.UseNewtonsoftJsonSerializer();

#pragma warning disable CS0618
                cfg.UseInMemoryScheduler(context);
#pragma warning restore CS0618


                var connectionString = configuration["RabbitMQ:ConnectionString"];
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        "RabbitMQ:ConnectionString configuration is required. Please provide CloudAMQP connection string.");
                }


                cfg.Host(connectionString);

           
                EndpointConvention.Map<ReserveSeatCommand>(new Uri($"queue:{RabbitMqSettings.FlightReserveSeatQueue}"));
                EndpointConvention.Map<ProcessPaymentCommand>(new Uri($"queue:{RabbitMqSettings.PaymentProcessQueue}"));
                EndpointConvention.Map<ReleaseSeatCommand>(new Uri($"queue:{RabbitMqSettings.FlightReleaseSeatQueue}"));

                cfg.ReceiveEndpoint(RabbitMqSettings.ReservationSagaQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureSaga<ReservationState>(context);
                    e.ConfigureConsumer<ReservationConfirmedStatusConsumer>(context);
                    e.ConfigureConsumer<ReservationSeatFailedStatusConsumer>(context);
                    e.ConfigureConsumer<ReservationPaymentFailedStatusConsumer>(context);
                    e.ConfigureConsumer<ReservationTimedOutStatusConsumer>(context);
                });

                // Flight read model – FlightCreatedEvent (Notification ile aynı event, farklı queue)
                cfg.ReceiveEndpoint(RabbitMqSettings.ReservationFlightCreatedQueue, e =>
                {
                    e.ConfigureConsumer<FlightCreatedConsumer>(context);
                });
            });
        });
    }
}
