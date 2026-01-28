using MassTransit;

namespace SkySync.SagaStateMachine.StateInstances;

/// <summary>
/// Saga State Machine State
/// Reservation işleminin durumunu tutar
/// </summary>
public class ReservationState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    
    // Rezervasyon Bilgileri
    public Guid ReservationId { get; set; }
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    
    // Yolcu Bilgileri
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerSurname { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;
    
    // Saga Durumu
    public string CurrentState { get; set; } = string.Empty; // State Machine'in mevcut durumu
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? FlightReservedAt { get; set; }
    public DateTime? PaymentCompletedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Hata Bilgisi
    public string? ErrorMessage { get; set; }
}
