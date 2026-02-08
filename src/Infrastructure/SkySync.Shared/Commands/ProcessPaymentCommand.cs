namespace SkySync.Shared.Commands;

/// <summary>
/// Payment Service'e gönderilecek command - Ödeme işlemini başlat
/// Saga State Machine tarafından publish edilir
/// </summary>
public class ProcessPaymentCommand
{
    public Guid CorrelationId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public string PassengerEmail { get; set; } = string.Empty;
    /// <summary>
    /// Ödeme bu zamana kadar tamamlanmalı. Aşılırsa komut geçersiz (timeout).
    /// Sıra garantisi olmadan da çalışır - Payment geç açılsa bile.
    /// </summary>
    public DateTime ValidUntil { get; set; }
}
