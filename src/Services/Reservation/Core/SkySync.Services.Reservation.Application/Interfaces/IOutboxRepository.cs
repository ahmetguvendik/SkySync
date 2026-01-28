namespace SkySync.Services.Reservation.Application.Interfaces;

public interface IOutboxRepository
{
    Task CreateAsync(SkySync.Shared.OutboxTable.OutboxMessage message, CancellationToken cancellationToken = default);
    Task<List<SkySync.Shared.OutboxTable.OutboxMessage>> GetUnprocessedMessagesAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(SkySync.Shared.OutboxTable.OutboxMessage message, CancellationToken cancellationToken = default);
}
