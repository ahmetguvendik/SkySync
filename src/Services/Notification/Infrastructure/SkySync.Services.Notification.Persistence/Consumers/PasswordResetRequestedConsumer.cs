using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

/// <summary>
/// Identity servisinden gelen PasswordResetRequestedEvent'i consume eder, e-posta gönderir.
/// </summary>
public class PasswordResetRequestedConsumer : IConsumer<PasswordResetRequestedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<PasswordResetRequestedConsumer> _logger;

    public PasswordResetRequestedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<PasswordResetRequestedConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PasswordResetRequestedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = message.Token;

        _logger.LogInformation(
            "PasswordResetRequested event received. MessageId: {MessageId}, Email: {Email}, UserId: {UserId}",
            messageId, message.Email, message.UserId);

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(PasswordResetRequestedEvent),
            businessKey,
            JsonSerializer.Serialize(message),
            async ct => await SendPasswordResetEmailAsync(message, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("Duplicate password reset event skipped. Token: {Token}", message.Token);
        }
    }

    private async Task SendPasswordResetEmailAsync(PasswordResetRequestedEvent message, CancellationToken cancellationToken)
    {
        var subject = "SkySync - Şifre Sıfırlama Talebi";
        var body = $@"
            <h1>Merhaba {message.FirstName} {message.LastName},</h1>
            <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanabilirsiniz. Bu bağlantı {message.ExpiresAt.ToLocalTime():dd.MM.yyyy HH:mm} tarihine kadar geçerlidir.</p>
            <p><a href=""{message.ResetLink}"">Şifremi Sıfırla</a></p>
            <p>Eğer bu isteği siz göndermediyseniz lütfen bu e-postayı dikkate almayın.</p>
            <p>SkySync Ekibi</p>";

        await _emailService.SendEmailAsync(message.Email, subject, body);
        _logger.LogInformation("Password reset email sent to {Email}", message.Email);
    }
}
