namespace SkySync.Services.Notification.Domain.Entities;

/// <summary>
/// Inbox Pattern - Duplicate event handling için
/// Consumer tarafında idempotency garantisi
/// Business Key bazlı idempotency (FlightId, ReservationId vs.)
/// </summary>
public class InboxMessage
{
    /// <summary>
    /// MessageId (RabbitMQ/MassTransit'ten gelen unique ID)
    /// Primary Key
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Business Key - İş mantığı bazlı unique key
    /// FlightCreatedEvent → FlightId
    /// ReservationConfirmedEvent → ReservationId
    /// </summary>
    public string BusinessKey { get; set; } = string.Empty;

    /// <summary>
    /// Event tipi (FlightCreatedEvent, ReservationConfirmedEvent, vs.)
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// İşlenme zamanı
    /// </summary>
    public DateTime ProcessedAt { get; set; }

    /// <summary>
    /// İşlenme durumu (Processed, Failed, Skipped)
    /// </summary>
    public string Status { get; set; } = "Processed";

    /// <summary>
    /// Event payload (debugging için - optional)
    /// </summary>
    public string? EventPayload { get; set; }

    /// <summary>
    /// Hata mesajı (failed durumunda)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Retry sayısı (eğer retry varsa)
    /// </summary>
    public int RetryCount { get; set; }
}
