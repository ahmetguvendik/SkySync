using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;

namespace SkySync.Services.Notification.Persistence.Consumers;

/// <summary>
/// ReservationConfirmedEvent consumer - Inbox Pattern ile duplicate handling
/// </summary>
public class ReservationConfirmedConsumer : IConsumer<ReservationConfirmedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ReservationConfirmedConsumer> _logger;

    public ReservationConfirmedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<ReservationConfirmedConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReservationConfirmedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _logger.LogInformation(
            "📩 ReservationConfirmed event received. MessageId: {MessageId}, Email: {Email}, ReservationId: {ReservationId}",
            messageId, msg.PassengerEmail, msg.ReservationId);

        // ✅ IDEMPOTENCY MARKER: Önce inbox'a kaydet (email göndermeden ÖNCE)
        var businessKey = msg.ReservationId.ToString();
        var markedSuccess = await _inboxService.MarkAsProcessedAsync(
            messageId,
            nameof(ReservationConfirmedEvent),
            businessKey,
            JsonSerializer.Serialize(msg));

        if (!markedSuccess)
        {
            // Duplicate yakalandı (başka consumer zaten işledi/işliyor)
            _logger.LogWarning(
                "⏭️  Duplicate event detected, skipping. ReservationId: {ReservationId}, MessageId: {MessageId}",
                msg.ReservationId, messageId);
            return;
        }

        _logger.LogInformation(
            "✅ Event locked for processing. ReservationId: {ReservationId}, MessageId: {MessageId}",
            msg.ReservationId, messageId);

        try
        {
            var subject = "SkySync - Biletiniz Hazır!";
            var body = $@"
                <h1>Sayın {msg.PassengerName} {msg.PassengerSurname},</h1>
                <p>Rezervasyonunuz başarıyla tamamlanmıştır.</p>
                <p><strong>Uçuş Bilgileri:</strong></p>
                <ul>
                    <li>Rezervasyon ID: {msg.ReservationId}</li>
                    <li>Uçuş ID: {msg.FlightId}</li>
                    <li>Koltuk: {msg.SeatNumber}</li>
                    <li>Tutar: {msg.Price} TL</li>
                </ul>
                <p>Bizi tercih ettiğiniz için teşekkür ederiz!</p>
                <p><em>SkySync Ekibi</em></p>";

            await _emailService.SendEmailAsync(msg.PassengerEmail, subject, body);

            _logger.LogInformation("Reservation confirmation email sent to {Email}", msg.PassengerEmail);

            // Not: Inbox'a zaten kayıt yapıldı (idempotency marker olarak)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending reservation confirmation email. MessageId: {MessageId}, Email: {Email}, ReservationId: {ReservationId}",
                messageId, msg.PassengerEmail, msg.ReservationId);

            // ❌ INBOX PATTERN: Hatalı olarak kaydet
            await _inboxService.MarkAsFailedAsync(
                messageId,
                nameof(ReservationConfirmedEvent),
                businessKey,
                ex.Message,
                JsonSerializer.Serialize(msg));

            // Exception'ı tekrar fırlat (MassTransit retry veya DLQ'ya gönderir)
            throw;
        }
    }
}
