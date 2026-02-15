namespace SkySync.Shared.OutboxTable;

public class OutboxMessage
{
    public Guid Id { get; set; } // Birincil anahtar
    public string Type { get; set; } = string.Empty; // Event tipi (örn: "FlightReservedEvent")
    public string Content { get; set; } = string.Empty; // JSON formatında mesaj içeriği
    public DateTime OccurredOn { get; set; } // Event'in oluşma zamanı
    public DateTime? ProcessedOn { get; set; } // Ne zaman kuyruğa gönderildi? (Null ise gönderilmedi)
    public string? Error { get; set; } // Gönderim sırasında bir hata oluştu mu?
    public int RetryCount { get; set; } = 0; // Kaç kez denendi?
    public bool IsFailed { get; set; } = false; // Max retry sonrası başarısız olarak işaretlendi mi?

    /// <summary>W3C traceparent - Distributed tracing için (OpenTelemetry)</summary>
    public string? Traceparent { get; set; }
    /// <summary>W3C tracestate - Distributed tracing için (OpenTelemetry)</summary>
    public string? Tracestate { get; set; }
}
