using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.InboxPattern;
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
        var businessKey = msg.ReservationId.ToString();

        _logger.LogInformation(
            "📩 ReservationConfirmed event received. MessageId: {MessageId}, Email: {Email}, ReservationId: {ReservationId}",
            messageId, msg.PassengerEmail, msg.ReservationId);

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(ReservationConfirmedEvent),
            businessKey,
            JsonSerializer.Serialize(msg),
            async ct => await SendReservationConfirmedEmailAsync(msg, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("⏭️ Duplicate ReservationConfirmed skipped. ReservationId: {ReservationId}, MessageId: {MessageId}", msg.ReservationId, messageId);
        }
    }

    private async Task SendReservationConfirmedEmailAsync(ReservationConfirmedEvent msg, CancellationToken ct)
    {
        var subject = "SkySync - Biletiniz Hazır!";
        var routeLine =
            !string.IsNullOrWhiteSpace(msg.FlightNumber) &&
            !string.IsNullOrWhiteSpace(msg.Departure) &&
            !string.IsNullOrWhiteSpace(msg.Destination)
                ? $"<li>Uçuş: {msg.FlightNumber} - {msg.Departure} → {msg.Destination}</li>"
                : string.Empty;
        var body = $@"
                <h1>Sayın {msg.PassengerName} {msg.PassengerSurname},</h1>
                <p>Rezervasyonunuz başarıyla tamamlanmıştır.</p>
                <p><strong>Uçuş Bilgileri:</strong></p>
                <ul>
                    {routeLine}
                    <li>Koltuk: {msg.SeatNumber}</li>
                    <li>Tutar: {msg.Price} TL</li>
                </ul>
                <p>Bizi tercih ettiğiniz için teşekkür ederiz!</p>
                <p><em>SkySync Ekibi</em></p>";

        await _emailService.SendEmailAsync(msg.PassengerEmail, subject, body);
        _logger.LogInformation("Reservation confirmation email sent to {Email}", msg.PassengerEmail);
    }
}
