namespace SkySync.Shared.Events;

/// <summary>
/// Flight Service'den gelen event - Koltuk başarıyla rezerve edildi
/// </summary>
public class FlightReservedEvent
{
    public Guid CorrelationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReservedAt { get; set; }
}
