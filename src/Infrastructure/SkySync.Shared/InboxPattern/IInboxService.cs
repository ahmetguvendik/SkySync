namespace SkySync.Shared.InboxPattern;

/// <summary>
/// Inbox Pattern Service Interface
/// Provides idempotent message processing capabilities for all services
/// </summary>
public interface IInboxService
{
    /// <summary>
    /// Check if event with this business key was already processed
    /// </summary>
    Task<bool> IsProcessedByBusinessKeyAsync(
        string eventType,
        string businessKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark event as processed (Idempotency Marker Pattern)
    /// Call this BEFORE doing the actual work
    /// Returns: true = successfully marked (proceed), false = duplicate (skip)
    /// </summary>
    Task<bool> MarkAsProcessedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string? eventPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark event as failed
    /// </summary>
    Task MarkAsFailedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string errorMessage,
        string? eventPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark event as skipped (duplicate detected)
    /// </summary>
    Task MarkAsSkippedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string? eventPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transactional Inbox: Begin Tx → Insert Processing → execute work → Update Completed → Commit.
    /// On any failure: Rollback, no Inbox row, rethrow (Nack). Returns false if duplicate (skip).
    /// </summary>
    Task<bool> TryProcessInTransactionAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string? eventPayload,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default);
}
