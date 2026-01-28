namespace SkySync.Shared.Commands;

/// <summary>
/// Flight Service'e gönderilecek command - Koltuk rezervasyonunu iptal et (Compensate)
/// Saga State Machine tarafından publish edilir (Payment failed durumunda)
/// </summary>
public class ReleaseSeatCommand
{
    public Guid CorrelationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
}
