using SkySync.Services.Reservation.Domain.Entities;
using SkySync.Services.Reservation.Domain.Enums;

namespace SkySync.Services.Reservation.Domain.Entities;

public class Reservation : BaseEntity
{
    public Guid FlightId { get; set; }
    public string SeatNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Yolcu Bilgileri
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerSurname { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;

    // Rezervasyon Durumu (Enum)
    public ReservationStatus Status { get; set; }

    // Takip için Saga CorrelationId
    public Guid CorrelationId { get; set; }
    public DateTime? ReminderSentAt { get; set; }
}
