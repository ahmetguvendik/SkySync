namespace SkySync.Services.Notification.Application.Interfaces;

/// <summary>
/// Inbox Pattern service - Duplicate event handling
/// Business Key bazlı idempotency
/// </summary>
public interface IInboxService
{
    /// <summary>
    /// Event daha önce işlendi mi kontrol et (MessageId bazlı - eski)
    /// </summary>
    Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event daha önce işlendi mi kontrol et (Business Key bazlı - yeni)
    /// </summary>
    Task<bool> IsProcessedByBusinessKeyAsync(
        string eventType, 
        string businessKey, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event'i işlenmiş olarak işaretle
    /// Returns: true = başarılı, false = duplicate (zaten var)
    /// </summary>
    Task<bool> MarkAsProcessedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string? eventPayload = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event'i hatalı olarak işaretle
    /// </summary>
    Task MarkAsFailedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        string errorMessage, 
        string? eventPayload = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event'i skip edilmiş olarak işaretle (duplicate)
    /// </summary>
    Task MarkAsSkippedAsync(
        Guid messageId,
        string eventType,
        string businessKey,
        CancellationToken cancellationToken = default);
}
