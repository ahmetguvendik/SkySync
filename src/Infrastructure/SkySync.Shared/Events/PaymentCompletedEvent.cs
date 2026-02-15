namespace SkySync.Shared.Events;

/// <summary>
/// Payment Service'den gelen event - Ödeme başarıyla tamamlandı
/// </summary>
public class PaymentCompletedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
