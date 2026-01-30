namespace SkySync.Shared.Events;

/// <summary>
/// Payment Service'den gelen event - Ödeme authorize edildi (para çekilmedi, sadece rezerve)
/// </summary>
public class PaymentAuthorizedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string AuthorizationId { get; set; } = string.Empty; // Payment gateway'den gelen authorization ID
    public DateTime AuthorizedAt { get; set; }
}
