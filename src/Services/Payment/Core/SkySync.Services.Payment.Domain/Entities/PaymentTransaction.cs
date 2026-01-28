namespace SkySync.Services.Payment.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Guid CorrelationId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Success, Failed, Pending
    public string? ErrorMessage { get; set; }
    public string? ExternalTransactionId { get; set; } // Bankadan dönen ID
}
