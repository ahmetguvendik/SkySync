using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Payment.Persistence.Contexts;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Payment.Persistence.Services;

/// <summary>
/// Inbox – Payment servisi. Duplicate ödeme işlemi önleme (kritik).
/// ProcessPayment, RefundPayment consumer'ları MarkAsProcessedAsync kullanır.
/// </summary>
public class InboxService : IInboxService
{
    private readonly PaymentServiceDbContext _context;
    private readonly ILogger<InboxService> _logger;

    public InboxService(PaymentServiceDbContext context, ILogger<InboxService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsProcessedByBusinessKeyAsync(string eventType, string businessKey, CancellationToken cancellationToken = default)
    {
        return await _context.InboxMessages.AnyAsync(
            i => i.EventType == eventType && i.BusinessKey == businessKey, cancellationToken);
    }

    public async Task<bool> MarkAsProcessedAsync(
        Guid messageId, string eventType, string businessKey, string? eventPayload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.InboxMessages.AddAsync(
                CreateMessage(messageId, eventType, businessKey, eventPayload, InboxStatus.Processed), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked processed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            _logger.LogWarning("Duplicate blocked. EventType: {EventType}, BusinessKey: {BusinessKey}", eventType, businessKey);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkAsProcessed failed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
            throw;
        }
    }

    public async Task MarkAsFailedAsync(
        Guid messageId, string eventType, string businessKey, string errorMessage, string? eventPayload = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var m = CreateMessage(messageId, eventType, businessKey, eventPayload, InboxStatus.Failed);
            m.ErrorMessage = errorMessage;
            await _context.InboxMessages.AddAsync(m, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Marked failed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}, Error: {Error}",
                messageId, eventType, businessKey, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkAsFailed write failed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
        }
    }

    public async Task MarkAsSkippedAsync(
        Guid messageId, string eventType, string businessKey, string? eventPayload = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.InboxMessages.AddAsync(
                CreateMessage(messageId, eventType, businessKey, eventPayload, InboxStatus.Skipped), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Marked skipped. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkAsSkipped failed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
        }
    }

    public Task<bool> TryProcessInTransactionAsync(Guid messageId, string eventType, string businessKey,
        string? eventPayload, Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Transactional Inbox yalnızca Notification serviste kullanılıyor.");

    private static InboxMessage CreateMessage(Guid messageId, string eventType, string businessKey, string? eventPayload, InboxStatus status)
    {
        return new InboxMessage
        {
            MessageId = messageId,
            EventType = eventType,
            BusinessKey = businessKey,
            EventPayload = eventPayload,
            Status = status,
            ProcessedAt = DateTime.UtcNow,
            RetryCount = 0
        };
    }
}
