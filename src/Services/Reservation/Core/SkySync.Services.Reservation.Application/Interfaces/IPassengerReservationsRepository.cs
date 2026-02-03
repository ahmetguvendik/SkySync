using SkySync.Services.Reservation.Application.DTOs;

namespace SkySync.Services.Reservation.Application.Interfaces;

/// <summary>
/// Yolcu email'ine göre rezervasyonları FlightSummary ile join ederek döndürür (FlightNumber dahil).
/// </summary>
public interface IPassengerReservationsRepository
{
    Task<List<ReservationDto>> GetByPassengerEmailAsync(string passengerEmail, CancellationToken cancellationToken = default);
}
