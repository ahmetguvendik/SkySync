namespace SkySync.Shared.Events;

/// <summary>
/// Payment Service'den gelen event - Ödeme başarıyla tamamlandı
/// </summary>
public class PaymentCompletedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; }
    public string TransactionId { get; set; }
    public DateTime CompletedAt { get; set; }
}
