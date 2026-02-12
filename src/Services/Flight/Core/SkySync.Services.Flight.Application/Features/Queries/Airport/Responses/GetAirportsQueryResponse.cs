using SkySync.Services.Flight.Application.DTOs;

namespace SkySync.Services.Flight.Application.Features.Queries.Airport.Responses;

public class GetAirportsQueryResponse
{
    public List<AirportDto> Airports { get; set; } = new();
}
