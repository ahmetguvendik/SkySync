using SkySync.Services.Reservation.Application.DTOs;

namespace SkySync.Services.Reservation.Application.Features.Queries.Reservation.Responses;

public class GetPassengerReservationsQueryResponse
{
    public List<ReservationDto> Reservations { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
