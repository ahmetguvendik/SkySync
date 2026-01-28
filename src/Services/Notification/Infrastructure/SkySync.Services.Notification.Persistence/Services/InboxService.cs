using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Services.Notification.Domain.Entities;
using SkySync.Services.Notification.Persistence.Contexts;

namespace SkySync.Services.Notification.Persistence.Services;

/// <summary>
/// Inbox Pattern implementation - PostgreSQL based
/// Duplicate event handling ve idempotency garantisi
/// </summary>
public class InboxService : IInboxService
{
    private readonly NotificationServiceDbContext _context;
    private readonly ILogger<InboxService> _logger;

    public InboxService(NotificationServiceDbContext context, ILogger<InboxService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _context.InboxMessages
                .AnyAsync(x => x.MessageId == messageId, cancellationToken);

            if (exists)
            {
                _logger.LogInformation("Message already processed. MessageId: {MessageId}", messageId);
            }

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if message is processed. MessageId: {MessageId}", messageId);
            // Hata durumunda false döndür, işleme devam etsin (at-least-once semantics)
            return false;
        }
    }

    public async Task<bool> IsProcessedByBusinessKeyAsync(
        string eventType,
        string businessKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _context.InboxMessages
                .AnyAsync(x => x.EventType == eventType && x.BusinessKey == businessKey, cancellationToken);

            if (exists)
            {
                _logger.LogInformation(
                    "Event already processed by business key. EventType: {EventType}, BusinessKey: {BusinessKey}",
                    eventType, businessKey);
            }

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking if event is processed by business key. EventType: {EventType}, BusinessKey: {BusinessKey}",
                eventType, businessKey);
            // Hata durumunda false döndür, işleme devam etsin
            return false;
        }
    }

    public async Task<bool> MarkAsProcessedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string? eventPayload = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                BusinessKey = businessKey,
                EventType = eventType,
                ProcessedAt = DateTime.UtcNow,
                Status = "Processed",
                EventPayload = eventPayload,
                RetryCount = 0
            };

            await _context.InboxMessages.AddAsync(inboxMessage, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "✅ Message marked as processed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);

            return true; // Başarılı
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            // Duplicate key error - başka bir instance zaten eklemiş (race condition)
            _logger.LogWarning(
                "⚠️ Event already exists in inbox (race condition - duplicate detected). EventType: {EventType}, BusinessKey: {BusinessKey}",
                eventType, businessKey);

            return false; // Duplicate!
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "❌ Error marking message as processed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);

            return false; // Hata
        }
    }

    public async Task MarkAsFailedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string errorMessage, 
        string? eventPayload = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                BusinessKey = businessKey,
                EventType = eventType,
                ProcessedAt = DateTime.UtcNow,
                Status = "Failed",
                EventPayload = eventPayload,
                ErrorMessage = errorMessage,
                RetryCount = 0
            };

            await _context.InboxMessages.AddAsync(inboxMessage, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Message marked as failed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}, Error: {Error}",
                messageId, eventType, businessKey, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error marking message as failed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
        }
    }

    public async Task MarkAsSkippedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Duplicate olduğu için skip edildi, ama inbox'a kaydet (tracking için)
            var inboxMessage = new InboxMessage
            {
                MessageId = messageId,
                BusinessKey = businessKey,
                EventType = eventType,
                ProcessedAt = DateTime.UtcNow,
                Status = "Skipped",
                RetryCount = 0
            };

            await _context.InboxMessages.AddAsync(inboxMessage, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Message marked as skipped (duplicate). MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error marking message as skipped. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
        }
    }
}
