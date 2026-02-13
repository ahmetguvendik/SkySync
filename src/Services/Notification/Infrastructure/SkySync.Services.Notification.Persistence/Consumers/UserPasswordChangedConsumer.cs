using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

public class UserPasswordChangedConsumer : IConsumer<UserPasswordChangedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<UserPasswordChangedConsumer> _logger;

    public UserPasswordChangedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<UserPasswordChangedConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserPasswordChangedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = $"{message.UserId}-{message.ChangedAt:O}";

        _logger.LogInformation("UserPasswordChangedEvent received. UserId: {UserId}", message.UserId);

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(UserPasswordChangedEvent),
            businessKey,
            JsonSerializer.Serialize(message),
            async ct => await SendPasswordChangedEmailAsync(message, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("Duplicate UserPasswordChangedEvent skipped. BusinessKey: {BusinessKey}", businessKey);
        }
    }

    private async Task SendPasswordChangedEmailAsync(UserPasswordChangedEvent message, CancellationToken cancellationToken)
    {
        var subject = "SkySync - Şifreniz değiştirildi";
        var body = $@"
            <h1>Merhaba,</h1>
            <p>{message.ChangedAt.ToLocalTime():dd.MM.yyyy HH:mm} tarihinde SkySync hesabınızın şifresi değiştirildi.</p>
            <p>Eğer bu işlemi siz yapmadıysanız hesabınızı güvenceye almak için lütfen hemen şifrenizi sıfırlayın.</p>
            <p>SkySync Ekibi</p>";

        await _emailService.SendEmailAsync(message.Email, subject, body);
        _logger.LogInformation("Password change email sent to {Email}", message.Email);
    }
}
