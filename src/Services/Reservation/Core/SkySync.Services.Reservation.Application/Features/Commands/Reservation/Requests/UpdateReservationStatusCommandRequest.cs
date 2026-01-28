using MediatR;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Responses;
using SkySync.Services.Reservation.Domain.Enums;

namespace SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;

public class UpdateReservationStatusCommandRequest : IRequest<UpdateReservationStatusCommandResponse>
{
    public Guid ReservationId { get; set; }
    public ReservationStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
