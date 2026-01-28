using MediatR;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Responses;

namespace SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;

public class CreateReservationCommandRequest : IRequest<CreateReservationCommandResponse>
{
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; }
    public decimal Price { get; set; }
    public string PassengerName { get; set; }
    public string PassengerSurname { get; set; }
    public string PassengerEmail { get; set; }
}
