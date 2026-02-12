using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Application.UnitOfWorks;
using SkySync.Services.Reservation.Domain.Enums;
using SkySync.Services.Reservation.Persistence.Contexts;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using System.Text.Json;

namespace SkySync.Services.Reservation.Persistence.Jobs;

public class FlightReminderOptions
{
    public int LeadTimeHours { get; set; } = 12;
    public int CheckIntervalMinutes { get; set; } = 15;
}

public class FlightReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FlightReminderBackgroundService> _logger;
    private readonly FlightReminderOptions _options;

    public FlightReminderBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<FlightReminderOptions> options,
        ILogger<FlightReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlightReminderBackgroundService started. Interval: {Interval} minutes, lead time: {LeadTime}h",
            _options.CheckIntervalMinutes, _options.LeadTimeHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing flight reminders");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.CheckIntervalMinutes), stoppingToken);
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReservationServiceDbContext>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var targetStart = now.AddHours(_options.LeadTimeHours).AddMinutes(-5);
        var targetEnd = now.AddHours(_options.LeadTimeHours).AddMinutes(5);

        var reminders = await (from reservation in dbContext.Reservations
                               join flight in dbContext.FlightSummaries on reservation.FlightId equals flight.FlightId
                               where !reservation.IsDeleted
                                     && reservation.Status == ReservationStatus.Confirmed
                                     && reservation.ReminderSentAt == null
                                     && flight.DepartureTime >= targetStart
                                     && flight.DepartureTime <= targetEnd
                               select new { reservation, flight })
            .ToListAsync(cancellationToken);

        if (!reminders.Any())
        {
            _logger.LogDebug("No reservations found for reminder window {Start} - {End}", targetStart, targetEnd);
            return;
        }

        foreach (var item in reminders)
        {
            var reminderEvent = new FlightReminderEvent
            {
                ReservationId = item.reservation.Id,
                FlightId = item.flight.FlightId,
                FlightNumber = item.flight.FlightNumber,
                DepartureTime = item.flight.DepartureTime,
                Departure = item.flight.Departure,
                Destination = item.flight.Destination,
                PassengerName = item.reservation.PassengerName,
                PassengerSurname = item.reservation.PassengerSurname,
                PassengerEmail = item.reservation.PassengerEmail
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(FlightReminderEvent),
                Content = JsonSerializer.Serialize(reminderEvent),
                OccurredOn = DateTime.UtcNow
            };

            await outboxRepository.CreateAsync(outboxMessage, cancellationToken);
            item.reservation.ReminderSentAt = DateTime.UtcNow;
            dbContext.Reservations.Update(item.reservation);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Flight reminders scheduled for {Count} reservations", reminders.Count);
    }
}
