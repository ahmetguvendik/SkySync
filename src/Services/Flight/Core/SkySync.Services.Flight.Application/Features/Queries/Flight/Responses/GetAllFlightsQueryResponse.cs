using SkySync.Services.Flight.Application.DTOs;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

public class GetAllFlightsQueryResponse
{
    public List<FlightDto> Flights { get; set; } = new();
    public bool IsFromCache { get; set; }
    public int TotalCount { get; set; }
}

