using SkySync.Services.Flight.Domain.Entities;

namespace SkySync.Services.Flight.Application.Interfaces;

public interface IAircraftRepository
{
    Task<Aircraft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Aircraft>> GetAllAsync(CancellationToken cancellationToken = default);
}
