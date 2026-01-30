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
/// Ödeme 5 dk gelmedi (timeout) → Reservation Failed. Inbox ile duplicate engellenir.
/// </summary>
public class ReservationTimedOutStatusConsumer : IConsumer<ReservationTimedOutEvent>
{
    private readonly ReservationServiceDbContext _db;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ReservationTimedOutStatusConsumer> _logger;

    public ReservationTimedOutStatusConsumer(
        ReservationServiceDbContext db,
        IInboxService inboxService,
        ILogger<ReservationTimedOutStatusConsumer> logger)
    {
        _db = db;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReservationTimedOutEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.CorrelationId.ToString();

        var marked = await _inboxService.MarkAsProcessedAsync(
            messageId, nameof(ReservationTimedOutEvent), businessKey, JsonSerializer.Serialize(msg), context.CancellationToken);
        if (!marked)
        {
            _logger.LogWarning("Duplicate ReservationTimedOutEvent skipped. CorrelationId: {CorrelationId}", msg.CorrelationId);
            return;
        }

        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(r => r.CorrelationId == msg.CorrelationId && !r.IsDeleted, context.CancellationToken);
        if (reservation == null)
        {
            _logger.LogWarning("ReservationTimedOutStatusConsumer: Reservation not found. CorrelationId: {CorrelationId}", msg.CorrelationId);
            return;
        }

        reservation.Status = ReservationStatus.Failed;
        reservation.ModifiedTime = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);
        _logger.LogInformation("Reservation Failed (timeout). ReservationId: {ReservationId}", reservation.Id);
    }
}
