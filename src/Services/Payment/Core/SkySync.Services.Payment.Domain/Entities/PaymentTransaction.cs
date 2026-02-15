using SkySync.Services.Payment.Domain.Enums;

namespace SkySync.Services.Payment.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public Guid ReservationId { get; set; }
    public Guid CorrelationId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExternalTransactionId { get; set; } // Bankadan dönen ID
}
