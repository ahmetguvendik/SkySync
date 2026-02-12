namespace SkySync.Services.Flight.Application.Features.Commands.Airport.Responses;

public class CreateAirportCommandResponse
{
    public Guid AirportId { get; set; }
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
