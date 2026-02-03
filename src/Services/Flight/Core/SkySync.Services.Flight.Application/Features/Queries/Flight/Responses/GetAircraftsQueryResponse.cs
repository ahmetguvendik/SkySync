using SkySync.Services.Flight.Application.DTOs;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

public class GetAircraftsQueryResponse
{
    public List<AircraftDto> Aircraft { get; set; } = new();
}
