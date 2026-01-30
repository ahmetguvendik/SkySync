using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Domain.Enums;
using SkySync.Services.Reservation.Persistence.Contexts;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Reservation.Persistence.Consumers;

/// <summary>
/// Uçak koltuğu rezerve edilemediğinde Reservation kaydını Failed yapar.
/// (FlightReservationFailedEvent) Inbox ile duplicate engellenir.
/// </summary>
public class ReservationSeatFailedStatusConsumer : IConsumer<FlightReservationFailedEvent>
{
    private readonly ReservationServiceDbContext _db;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ReservationSeatFailedStatusConsumer> _logger;

    public ReservationSeatFailedStatusConsumer(
        ReservationServiceDbContext db,
        IInboxService inboxService,
        ILogger<ReservationSeatFailedStatusConsumer> logger)
    {
        _db = db;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FlightReservationFailedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.CorrelationId.ToString();

        var marked = await _inboxService.MarkAsProcessedAsync(
            messageId, nameof(FlightReservationFailedEvent), businessKey, JsonSerializer.Serialize(msg), context.CancellationToken);
        if (!marked)
        {
            _logger.LogWarning("Duplicate FlightReservationFailedEvent skipped. CorrelationId: {CorrelationId}", msg.CorrelationId);
            return;
        }

        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(r => r.CorrelationId == msg.CorrelationId && !r.IsDeleted, context.CancellationToken);

        if (reservation == null)
        {
            _logger.LogWarning(
                "ReservationSeatFailedStatusConsumer: Reservation not found. CorrelationId: {CorrelationId}",
                msg.CorrelationId);
            return;
        }

        reservation.Status = ReservationStatus.Failed;
        reservation.ModifiedTime = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Reservation status updated to Failed (seat). ReservationId: {ReservationId}",
            reservation.Id);
    }
}
