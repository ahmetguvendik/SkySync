namespace SkySync.Shared.Events;

/// <summary>
/// Payment Service'den gelen event - Ödeme capture edildi (para çekildi)
/// </summary>
public class PaymentCapturedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
}
