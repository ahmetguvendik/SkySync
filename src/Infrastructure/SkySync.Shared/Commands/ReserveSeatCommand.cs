namespace SkySync.Shared.Commands;

/// <summary>
/// Flight Service'e gönderilecek command - Koltuk rezerve et
/// Saga State Machine tarafından publish edilir
/// </summary>
public class ReserveSeatCommand
{
    public Guid CorrelationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
}
