using SkySync.Services.Notification.Domain.Entities;

namespace SkySync.Services.Notification.Application.Interfaces;

public interface INotificationUserRepository
{
    Task UpsertAsync(NotificationUser user, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(Guid userId, string email, string firstName, string lastName, DateTime updatedAt, CancellationToken cancellationToken = default);
    Task<bool> UpdateOperationalPreferenceAsync(Guid userId, bool receivesOperationalEmails, CancellationToken cancellationToken = default);
    Task<NotificationUser?> GetByUnsubscribeTokenAsync(Guid token, CancellationToken cancellationToken = default);
    Task RegenerateUnsubscribeTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationUser>> GetOperationalContactsAsync(CancellationToken cancellationToken = default);
}
