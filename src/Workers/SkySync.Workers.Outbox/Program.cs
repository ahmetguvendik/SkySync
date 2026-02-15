using System.Collections.Generic;
using MassTransit;
using MassTransit.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using FlightPersistence = SkySync.Services.Flight.Persistence;
using ReservationPersistence = SkySync.Services.Reservation.Persistence;
using IdentityPersistence = SkySync.Services.Identity.Persistence;
using SkySync.Workers.Outbox.Jobs.Flight;
using SkySync.Workers.Outbox.Jobs.Reservation;
using SkySync.Workers.Outbox.Jobs.Identity;

var builder = Host.CreateApplicationBuilder(args);

// Add Persistence Services (Flight Service DbContext ve Repositories)
FlightPersistence.ServiceRegistration.AddPersistenceService(builder.Services, builder.Configuration);

// Add Persistence Services (Reservation Service DbContext ve Repositories)
ReservationPersistence.ServiceRegistration.AddPersistenceService(builder.Services, builder.Configuration);

// Add Persistence Services (Identity Service DbContext ve Repositories)
IdentityPersistence.ServiceRegistration.AddPersistenceServices(builder.Services, builder.Configuration);

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        // CRITICAL: .NET 9 uyumluluğu için Newtonsoft JSON serializer kullan
        cfg.UseNewtonsoftJsonSerializer();

        // CloudAMQP connection string kullan
        var connectionString = builder.Configuration["RabbitMQ:ConnectionString"];

        if (!string.IsNullOrEmpty(connectionString))
        {
            // CloudAMQP connection string formatı: amqps://username:password@host/vhost
            cfg.Host(connectionString);
        }
        else
        {
            // Fallback: Local RabbitMQ için
            var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            var rabbitMqUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
            var rabbitMqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";
            var rabbitMqVHost = builder.Configuration["RabbitMQ:VHost"] ?? "/";

            cfg.Host(rabbitMqHost, rabbitMqVHost, h =>
            {
                h.Username(rabbitMqUsername);
                h.Password(rabbitMqPassword);
            });
        }

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            builder.Configuration["OpenTelemetry:ServiceName"] ?? "SkySync-OutboxWorker",
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            serviceInstanceId: Environment.MachineName)
        .AddAttributes(new Dictionary<string, object> { ["deployment.environment"] = builder.Environment.EnvironmentName }))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(DiagnosticHeaders.DefaultListenerName)  // MassTransit mesaj trace
            .AddEntityFrameworkCoreInstrumentation()  // Outbox DB okuma
            .AddOtlpExporter(options =>
            {
                var endpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
                options.Endpoint = new Uri(endpoint);
            });
    });

// Add Outbox Workers (Flight ve Reservation için ayrı worker'lar)
builder.Services.AddHostedService<FlightOutboxPublishWorker>();
builder.Services.AddHostedService<ReservationOutboxPublishWorker>();
builder.Services.AddHostedService<IdentityOutboxPublishWorker>();

var host = builder.Build();
host.Run();