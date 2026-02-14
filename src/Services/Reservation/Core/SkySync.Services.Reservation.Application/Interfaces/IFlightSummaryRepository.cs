using SkySync.Services.Reservation.Domain.Entities;

namespace SkySync.Services.Reservation.Application.Interfaces;

/// <summary>
/// Read model repository for flight metadata to validate reservation operations.
/// </summary>
public interface IFlightSummaryRepository
{
    Task<FlightSummary?> GetByFlightIdAsync(Guid flightId, CancellationToken cancellationToken = default);
}
