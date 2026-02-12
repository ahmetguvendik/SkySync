using MediatR;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Responses;

namespace SkySync.Services.Flight.Application.Features.Commands.Airport.Requests;

public class CreateAirportCommandRequest : IRequest<CreateAirportCommandResponse>
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
