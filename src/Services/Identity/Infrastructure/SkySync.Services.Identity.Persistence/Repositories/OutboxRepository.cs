using Microsoft.EntityFrameworkCore;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Persistence.Contexts;
using SkySync.Shared.OutboxTable;

namespace SkySync.Services.Identity.Persistence.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly IdentityServiceDbContext _context;

    public OutboxRepository(IdentityServiceDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    
    public async Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.OutboxMessages
            .Where(x => x.ProcessedOn == null && !x.IsFailed)
            .OrderBy(x => x.OccurredOn)
            .ToListAsync(cancellationToken);
    }

    public Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _context.OutboxMessages.Update(message);
        return Task.CompletedTask;
    }
}
