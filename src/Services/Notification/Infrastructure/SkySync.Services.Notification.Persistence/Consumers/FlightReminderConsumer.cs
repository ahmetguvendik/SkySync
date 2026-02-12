using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

public class FlightReminderConsumer : IConsumer<FlightReminderEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<FlightReminderConsumer> _logger;

    public FlightReminderConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<FlightReminderConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FlightReminderEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.ReservationId.ToString();

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(FlightReminderEvent),
            businessKey,
            JsonSerializer.Serialize(msg),
            async ct => await SendReminderAsync(msg, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("Duplicate flight reminder skipped. ReservationId: {ReservationId}", msg.ReservationId);
        }
    }

    private async Task SendReminderAsync(FlightReminderEvent msg, CancellationToken cancellationToken)
    {
        var subject = "SkySync - Uçuşunuz yaklaşıyor";
        var body = $@"
            <p>Merhaba {msg.PassengerName} {msg.PassengerSurname},</p>
            <p>{msg.Departure} → {msg.Destination} uçuşunuz {msg.DepartureTime:G} tarihinde gerçekleşecektir.</p>
            <p>Lütfen biletinizi ve gerekli belgelerinizi yanınıza almayı unutmayın.</p>
            <p>İyi yolculuklar!</p>";

        await _emailService.SendEmailAsync(msg.PassengerEmail, subject, body);
        _logger.LogInformation("Flight reminder email sent to {Email} for reservation {ReservationId}", msg.PassengerEmail, msg.ReservationId);
    }
}
