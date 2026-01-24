namespace SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;

public class CreateFlightCommandResponse
{
    public Guid FlightId { get; set; }
    public string FlightNumber { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
}