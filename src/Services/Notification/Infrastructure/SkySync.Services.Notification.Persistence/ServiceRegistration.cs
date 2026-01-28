using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Services.Notification.Persistence.Consumers;
using SkySync.Services.Notification.Persistence.Contexts;
using SkySync.Services.Notification.Persistence.Services;

namespace SkySync.Services.Notification.Persistence;

public static class ServiceRegistration
{
    public static void AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - PostgreSQL (Inbox Pattern için)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<NotificationServiceDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Services
        services.AddScoped<IEmailService, GmailEmailService>();
        services.AddScoped<IInboxService, InboxService>();

        // MassTransit
        services.AddMassTransit(x =>
        {
            x.AddConsumer<ReservationConfirmedConsumer>();
            x.AddConsumer<FlightCreatedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConnectionString = configuration["RabbitMQ:ConnectionString"];
                cfg.Host(rabbitMqConnectionString);

                cfg.ReceiveEndpoint("notification-confirmed-queue", e =>
                {
                    e.ConfigureConsumer<ReservationConfirmedConsumer>(context);
                });

                cfg.ReceiveEndpoint("notification-flight-created-queue", e =>
                {
                    e.ConfigureConsumer<FlightCreatedConsumer>(context);
                });
            });
        });
    }
}
