using MediatR;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Responses;

namespace SkySync.Services.Reservation.Application.Features.Queries.Reservation.Requests;

public class GetPassengerReservationsQueryRequest : IRequest<GetPassengerReservationsQueryResponse>
{
    public string PassengerEmail { get; set; }
}
