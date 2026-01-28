namespace SkySync.Services.Reservation.Application.Features.Commands.Reservation.Responses;

public class CreateReservationCommandResponse
{
    public Guid ReservationId { get; set; }
    public Guid CorrelationId { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
