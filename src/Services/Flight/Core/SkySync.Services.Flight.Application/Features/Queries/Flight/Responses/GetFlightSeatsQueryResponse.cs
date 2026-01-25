using SkySync.Services.Flight.Application.DTOs;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

public class GetFlightSeatsQueryResponse
{
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; } = string.Empty;
    public List<SeatDto> Seats { get; set; } = new();
    public int AvailableSeatsCount { get; set; }
    public int ReservedSeatsCount { get; set; }
    public int TotalSeatsCount { get; set; }
}
