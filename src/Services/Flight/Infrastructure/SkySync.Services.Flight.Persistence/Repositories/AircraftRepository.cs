using Microsoft.EntityFrameworkCore;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Domain.Entities;
using SkySync.Services.Flight.Persistence.Contexts;

namespace SkySync.Services.Flight.Persistence.Repositories;

public class AircraftRepository : IAircraftRepository
{
    private readonly FlightServiceDbContext _context;

    public AircraftRepository(FlightServiceDbContext context)
    {
        _context = context;
    }

    public async Task<Aircraft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Aircraft
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<List<Aircraft>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Aircraft
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);
    }
}
