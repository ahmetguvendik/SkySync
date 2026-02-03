using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Reservation.Persistence.Contexts;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Reservation.Persistence.Services;

/// <summary>
/// Inbox – Reservation servisi. Status consumer'ları MarkAsProcessedAsync kullanır.
/// FlightCreatedConsumer gibi tx gerektiren işler TryProcessInTransactionAsync kullanır.
/// </summary>
public class InboxService : IInboxService
{
    private readonly ReservationServiceDbContext _context;
    private readonly ILogger<InboxService> _logger;

    public InboxService(ReservationServiceDbContext context, ILogger<InboxService> logger)
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
            _logger.LogDebug("Marked processed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
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

    /// <summary>
    /// Tx başlat → Insert Processing → work() → Update Processed → Commit.
    /// FlightCreatedConsumer FlightSummary upsert'ini bu tx içinde yapar.
    /// </summary>
    public async Task<bool> TryProcessInTransactionAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string? eventPayload,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (await _context.InboxMessages.AnyAsync(
                    x => x.EventType == eventType && x.BusinessKey == businessKey, cancellationToken))
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogWarning("Duplicate skipped. EventType: {EventType}, BusinessKey: {BusinessKey}", eventType, businessKey);
                return false;
            }

            var inboxMessage = CreateMessage(messageId, eventType, businessKey, eventPayload, InboxStatus.Processing);
            await _context.InboxMessages.AddAsync(inboxMessage, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await work(cancellationToken);

            inboxMessage.Status = InboxStatus.Processed;
            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Inbox completed. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Inbox failed, rolled back. MessageId: {MessageId}, EventType: {EventType}, BusinessKey: {BusinessKey}",
                messageId, eventType, businessKey);
            throw;
        }
    }

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
