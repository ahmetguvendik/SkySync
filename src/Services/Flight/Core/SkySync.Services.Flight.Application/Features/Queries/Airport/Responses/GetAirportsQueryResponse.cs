using SkySync.Services.Flight.Application.DTOs;

namespace SkySync.Services.Flight.Application.Features.Queries.Airport.Responses;

public class GetAirportsQueryResponse
{
    public List<AirportDto> Airports { get; set; } = new();
    public bool IsFromCache { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
