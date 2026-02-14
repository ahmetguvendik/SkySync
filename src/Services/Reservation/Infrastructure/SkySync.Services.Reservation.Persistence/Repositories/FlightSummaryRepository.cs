using Microsoft.EntityFrameworkCore;
using SkySync.Services.Reservation.Application.Interfaces;
using SkySync.Services.Reservation.Domain.Entities;
using SkySync.Services.Reservation.Persistence.Contexts;

namespace SkySync.Services.Reservation.Persistence.Repositories;

public class FlightSummaryRepository : IFlightSummaryRepository
{
    private readonly ReservationServiceDbContext _context;

    public FlightSummaryRepository(ReservationServiceDbContext context)
    {
        _context = context;
    }

    public async Task<FlightSummary?> GetByFlightIdAsync(Guid flightId, CancellationToken cancellationToken = default)
    {
        return await _context.FlightSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FlightId == flightId, cancellationToken);
    }
}
