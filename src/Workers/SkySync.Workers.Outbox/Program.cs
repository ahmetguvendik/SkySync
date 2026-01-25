using MassTransit;
using SkySync.Services.Flight.Persistence;
using SkySync.Workers.Outbox.Jobs;

var builder = Host.CreateApplicationBuilder(args);

// Add Persistence Services (Flight Service DbContext ve Repositories)
builder.Services.AddPersistenceService(builder.Configuration);

// Add MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
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

// Add Outbox Worker
builder.Services.AddHostedService<FlightOutboxPublishWorker>();

var host = builder.Build();
host.Run();