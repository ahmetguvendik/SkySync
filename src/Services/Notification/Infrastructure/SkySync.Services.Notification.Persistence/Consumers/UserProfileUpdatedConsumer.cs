using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

public class UserProfileUpdatedConsumer : IConsumer<UserProfileUpdatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly ILogger<UserProfileUpdatedConsumer> _logger;

    public UserProfileUpdatedConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        ILogger<UserProfileUpdatedConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserProfileUpdatedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = $"{message.UserId}-{message.UpdatedAt:O}";

        _logger.LogInformation("UserProfileUpdatedEvent received. UserId: {UserId}, UpdatedFields: {Fields}", message.UserId, string.Join(",", message.UpdatedFields));

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(UserProfileUpdatedEvent),
            businessKey,
            JsonSerializer.Serialize(message),
            async ct => await SendProfileUpdatedEmailAsync(message, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning("Duplicate UserProfileUpdatedEvent skipped. BusinessKey: {BusinessKey}", businessKey);
        }
    }

    private async Task SendProfileUpdatedEmailAsync(UserProfileUpdatedEvent message, CancellationToken cancellationToken)
    {
        var subject = "SkySync - Profiliniz güncellendi";
        var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstName"] = "adınız",
            ["LastName"] = "soyadınız",
            ["Email"] = "e-posta adresiniz"
        };

        var updatedFieldsText = message.UpdatedFields.Any()
            ? string.Join(", ", message.UpdatedFields.Select(f => friendlyNames.TryGetValue(f, out var friendly) ? friendly : f))
            : "profil bilgileriniz";

        var body = $@"
            <h1>Merhaba {message.FirstName} {message.LastName},</h1>
            <p>{message.UpdatedAt.ToLocalTime():dd.MM.yyyy HH:mm} tarihinde {updatedFieldsText} güncellendi.</p>
            <p>Eğer bu işlemi siz yapmadıysanız lütfen hemen destek ekibiyle iletişime geçin.</p>
            <p>SkySync Ekibi</p>";

        await _emailService.SendEmailAsync(message.Email, subject, body);
        _logger.LogInformation("Profile update email sent to {Email}", message.Email);
    }
}
