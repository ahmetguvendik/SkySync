namespace SkySync.Services.Flight.Application.DTOs;

public class AircraftDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatCount { get; set; }
}
