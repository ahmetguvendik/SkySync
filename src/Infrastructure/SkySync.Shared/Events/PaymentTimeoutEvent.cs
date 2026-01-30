namespace SkySync.Shared.Events;

/// <summary>
/// Saga içindeki ödeme süresi dolduğunda yayınlanan event.
/// Payment service'ten değil, Reservation Saga'dan gelir.
/// </summary>
public class PaymentTimeoutEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TimeoutAt { get; set; }
    public string Reason { get; set; } = "Payment timeout";
}
