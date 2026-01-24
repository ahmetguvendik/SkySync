using MediatR;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;
using SkySync.Services.Flight.Domain.Enums;

namespace SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;

public class CreateFlightCommandRequest : IRequest<CreateFlightCommandResponse>
{
    public string FlightNumber { get; set; }
    public string Departure { get; set; }
    public string Destination { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal BasePrice { get; set; }
    public FlightStatus Status { get; set; } = FlightStatus.Active;
}