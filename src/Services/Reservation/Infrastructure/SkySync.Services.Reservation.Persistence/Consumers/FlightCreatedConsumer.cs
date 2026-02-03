using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Domain.Entities;
using SkySync.Services.Reservation.Persistence.Contexts;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Reservation.Persistence.Consumers;

/// <summary>
/// FlightCreatedEvent consumer – Reservation read model (FlightSummary) günceller.
/// Rezervasyon listesinde FlightNumber join ile dönmek için kullanılır.
/// Inbox ile duplicate event işlenmez.
/// </summary>
public class FlightCreatedConsumer : IConsumer<FlightCreatedEvent>
{
    private readonly ReservationServiceDbContext _db;
    private readonly IInboxService _inboxService;
    private readonly ILogger<FlightCreatedConsumer> _logger;

    public FlightCreatedConsumer(
        ReservationServiceDbContext db,
        IInboxService inboxService,
        ILogger<FlightCreatedConsumer> logger)
    {
        _db = db;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FlightCreatedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.FlightId.ToString();

        _logger.LogInformation(
            "FlightCreated received. FlightId: {FlightId}, FlightNumber: {FlightNumber}",
            msg.FlightId, msg.FlightNumber);

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(FlightCreatedEvent),
            businessKey,
            JsonSerializer.Serialize(msg),
            async ct => await UpsertFlightSummaryAsync(msg, ct),
            context.CancellationToken);

        if (!processed)
            _logger.LogWarning("Duplicate FlightCreated skipped. FlightId: {FlightId}", msg.FlightId);
    }

    private async Task UpsertFlightSummaryAsync(FlightCreatedEvent msg, CancellationToken ct)
    {
        var existing = await _db.FlightSummaries
            .FirstOrDefaultAsync(f => f.FlightId == msg.FlightId, ct);

        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.FlightNumber = msg.FlightNumber;
            existing.Departure = msg.Departure;
            existing.Destination = msg.Destination;
            existing.DepartureTime = msg.DepartureTime;
            existing.ArrivalTime = msg.ArrivalTime;
            existing.UpdatedAt = now;
        }
        else
        {
            _db.FlightSummaries.Add(new FlightSummary
            {
                FlightId = msg.FlightId,
                FlightNumber = msg.FlightNumber,
                Departure = msg.Departure,
                Destination = msg.Destination,
                DepartureTime = msg.DepartureTime,
                ArrivalTime = msg.ArrivalTime,
                UpdatedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);
    }
}
