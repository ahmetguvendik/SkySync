namespace SkySync.Shared.InboxPattern;

/// <summary>
/// Inbox Pattern - Ensures idempotent message processing across all services
/// Prevents duplicate processing from the same event (e.g., duplicate payments, duplicate emails)
/// 
/// Usage:
/// - Payment Service: Prevents duplicate payment charges
/// - Flight Service: Prevents duplicate seat reservations
/// - Notification Service: Prevents duplicate email sends
/// </summary>
public enum InboxStatus
{
    Processing = 0,
    Processed = 1,
    Failed = 2,
    Skipped = 3
}

public class InboxMessage
{
    /// <summary>
    /// Unique message identifier from MassTransit (Primary Key)
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Business-level unique key (e.g., ReservationId, FlightId)
    /// Used for idempotency check with EventType
    /// Unique constraint: (EventType, BusinessKey)
    /// </summary>
    public string BusinessKey { get; set; } = string.Empty;

    /// <summary>
    /// Event/Command type name (e.g., ProcessPaymentCommand, ReserveSeatCommand)
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload of the event (for debugging/auditing)
    /// </summary>
    public string? EventPayload { get; set; }

    /// <summary>
    /// Processing status (Processed, Failed, Skipped)
    /// </summary>
    public InboxStatus Status { get; set; } = InboxStatus.Processed;

    /// <summary>
    /// When the message was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Retry count for failed messages
    /// </summary>
    public int RetryCount { get; set; }
}
