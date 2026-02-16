using System;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Services.Notification.Domain.Entities;
using SkySync.Shared.Events;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Consumers;

/// <summary>
/// Identity servisinden gelen UserRegisteredEvent'i consume edip hoş geldin maili gönderir.
/// </summary>
public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly IEmailService _emailService;
    private readonly IInboxService _inboxService;
    private readonly INotificationUserRepository _notificationUserRepository;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(
        IEmailService emailService,
        IInboxService inboxService,
        INotificationUserRepository notificationUserRepository,
        ILogger<UserRegisteredConsumer> logger)
    {
        _emailService = emailService;
        _inboxService = inboxService;
        _notificationUserRepository = notificationUserRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();
        var businessKey = message.UserId.ToString();

        _logger.LogInformation(
            "UserRegistered event received. MessageId: {MessageId}, Email: {Email}, UserId: {UserId}",
            messageId, message.Email, message.UserId);

        var processed = await _inboxService.TryProcessInTransactionAsync(
            messageId,
            nameof(UserRegisteredEvent),
            businessKey,
            JsonSerializer.Serialize(message),
            async ct => await ProcessRegistrationAsync(message, ct),
            context.CancellationToken);

        if (!processed)
        {
            _logger.LogWarning(
                "Duplicate UserRegistered skipped for UserId: {UserId}, MessageId: {MessageId}",
                message.UserId,
                messageId);
        }
    }

    private async Task ProcessRegistrationAsync(UserRegisteredEvent message, CancellationToken cancellationToken)
    {
        var notificationUser = new NotificationUser
        {
            UserId = message.UserId,
            Email = message.Email,
            FirstName = message.FirstName,
            LastName = message.LastName,
            Role = message.Role,
            ReceivesOperationalEmails = message.ReceivesOperationalEmails,
            RegisteredAt = message.RegisteredAt,
            LastUpdatedAt = DateTime.UtcNow,
            UnsubscribeToken = Guid.NewGuid()
        };

        await _notificationUserRepository.UpsertAsync(notificationUser, cancellationToken);

        var subject = "SkySync - Aramıza Hoş Geldiniz";
        var body = $@"
            <h1>Merhaba {message.FirstName} {message.LastName},</h1>
            <p>SkySync hesabınız başarıyla oluşturuldu.</p>
            <p>Artık uçuşları keşfedebilir, rezervasyon yapabilir ve kampanyalardan haberdar olabilirsiniz.</p>
            <p>Keyifli uçuşlar dileriz!<br/>SkySync Ekibi</p>";

        await _emailService.SendEmailAsync(message.Email, subject, body);
        _logger.LogInformation("Welcome email sent to {Email}", message.Email);
    }
}
