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
/// Rezervasyon başarılı tamamlandığında (PaymentCompleted → ReservationConfirmedEvent)
/// Reservation tablosundaki satırı Confirmed yapar. Inbox ile duplicate engellenir.
/// </summary>
public class ReservationConfirmedStatusConsumer : IConsumer<ReservationConfirmedEvent>
{
    private readonly ReservationServiceDbContext _db;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ReservationConfirmedStatusConsumer> _logger;

    public ReservationConfirmedStatusConsumer(
        ReservationServiceDbContext db,
        IInboxService inboxService,
        ILogger<ReservationConfirmedStatusConsumer> logger)
    {
        _db = db;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReservationConfirmedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.ReservationId.ToString();

        var marked = await _inboxService.MarkAsProcessedAsync(
            messageId, nameof(ReservationConfirmedEvent), businessKey, JsonSerializer.Serialize(msg), context.CancellationToken);
        if (!marked)
        {
            _logger.LogWarning("Duplicate ReservationConfirmedEvent skipped. ReservationId: {ReservationId}", msg.ReservationId);
            return;
        }

        var reservation = await _db.Reservations
            .FirstOrDefaultAsync(r => r.Id == msg.ReservationId && !r.IsDeleted, context.CancellationToken);

        if (reservation == null)
        {
            _logger.LogWarning(
                "ReservationConfirmedStatusConsumer: Reservation not found. ReservationId: {ReservationId}",
                msg.ReservationId);
            return;
        }

        reservation.Status = ReservationStatus.Confirmed;
        reservation.ModifiedTime = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Reservation status updated to Confirmed. ReservationId: {ReservationId}",
            msg.ReservationId);
    }
}
