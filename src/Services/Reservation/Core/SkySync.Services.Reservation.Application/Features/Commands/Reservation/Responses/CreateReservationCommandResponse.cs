namespace SkySync.Services.Reservation.Application.Features.Commands.Reservation.Responses;

public class CreateReservationCommandResponse
{
    public Guid ReservationId { get; set; }
    public Guid CorrelationId { get; set; }
    public decimal Price { get; set; }
    /// <summary>
    /// Ödeme bu zamana kadar yapılmalı. Frontend POST /api/v1/payment/process ile ödemeyi tetikler.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
