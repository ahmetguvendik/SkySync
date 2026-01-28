namespace SkySync.Shared.Events;

/// <summary>
/// Payment Service'den gelen event - Ödeme başarısız oldu
/// </summary>
public class PaymentFailedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
}
