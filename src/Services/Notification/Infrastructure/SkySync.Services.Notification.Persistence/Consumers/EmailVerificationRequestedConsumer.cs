using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

public class EmailVerificationRequestedConsumer : IConsumer<EmailVerificationRequestedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<EmailVerificationRequestedConsumer> _logger;

    public EmailVerificationRequestedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<EmailVerificationRequestedConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EmailVerificationRequestedEvent> context)
    {
        var msg = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = msg.UserId.ToString();

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(EmailVerificationRequestedEvent),
            businessKey,
            JsonSerializer.Serialize(msg),
            async ct => await SendVerificationEmail(msg, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("Duplicate EmailVerificationRequested skipped. UserId: {UserId}", msg.UserId);
        }
    }

    private async Task SendVerificationEmail(EmailVerificationRequestedEvent msg, CancellationToken cancellationToken)
    {
        var subject = "SkySync - Email adresinizi doğrulayın";
        var link = string.IsNullOrWhiteSpace(msg.VerificationLink)
            ? $"Doğrulama kodu: {msg.VerificationToken}"
            : $"<a href=\"{msg.VerificationLink}\">Email adresinizi doğrulamak için buraya tıklayın</a>";
        var body = $@"
            <p>Merhaba {msg.FirstName} {msg.LastName},</p>
            <p>SkySync hesabınızı tamamlamak için email adresinizi doğrulamanız gerekiyor.</p>
            <p>{link}</p>
            <p>Bu bağlantı {msg.ExpiresAt:G} tarihine kadar geçerlidir.</p>";

        await _emailService.SendEmailAsync(msg.Email, subject, body);
        _logger.LogInformation("Verification email sent to {Email}", msg.Email);
    }
}
