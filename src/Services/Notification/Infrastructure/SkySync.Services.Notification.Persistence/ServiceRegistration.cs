using MassTransit;
using SkySync.Infrastructure.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Services.Notification.Persistence.Consumers;
using SkySync.Services.Notification.Persistence.Contexts;
using SkySync.Services.Notification.Persistence.Services;
using SkySync.Shared;
using SkySync.Shared.InboxPattern;

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
            x.AddConsumer<UserRegisteredConsumer>();
            x.AddConsumer<PasswordResetRequestedConsumer>();
            x.AddConsumer<EmailVerificationRequestedConsumer>();
            x.AddConsumer<FlightReminderConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitMqConnectionString = configuration["RabbitMQ:ConnectionString"];
                cfg.Host(rabbitMqConnectionString);
                cfg.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);

                cfg.ReceiveEndpoint(RabbitMqSettings.NotificationReservationConfirmedQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<ReservationConfirmedConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.NotificationFlightCreatedQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<FlightCreatedConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.NotificationUserRegisteredQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<UserRegisteredConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.NotificationPasswordResetQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<PasswordResetRequestedConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.NotificationEmailVerificationQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<EmailVerificationRequestedConsumer>(context);
                });

                cfg.ReceiveEndpoint(RabbitMqSettings.NotificationFlightReminderQueue, e =>
                {
                    e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    e.ConfigureConsumer<FlightReminderConsumer>(context);
                });
            });
        });
    }
}
