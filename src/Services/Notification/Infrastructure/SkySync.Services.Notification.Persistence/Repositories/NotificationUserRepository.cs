using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using SkySync.Services.Notification.Application.Interfaces;
using SkySync.Services.Notification.Domain.Entities;
using SkySync.Services.Notification.Persistence.Contexts;

namespace SkySync.Services.Notification.Persistence.Repositories;

public class NotificationUserRepository : INotificationUserRepository
{
    private readonly NotificationServiceDbContext _context;
    private readonly ILogger<NotificationUserRepository> _logger;

    public NotificationUserRepository(NotificationServiceDbContext context, ILogger<NotificationUserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertAsync(NotificationUser user, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NotificationUsers
            .FirstOrDefaultAsync(x => x.UserId == user.UserId, cancellationToken);

        if (user.UnsubscribeToken == Guid.Empty)
        {
            user.UnsubscribeToken = Guid.NewGuid();
        }

        if (entity == null)
        {
            user.LastUpdatedAt = user.LastUpdatedAt == default ? DateTime.UtcNow : user.LastUpdatedAt;
            await _context.NotificationUsers.AddAsync(user, cancellationToken);
        }
        else
        {
            entity.Email = user.Email;
            entity.FirstName = user.FirstName;
            entity.LastName = user.LastName;
            entity.Role = user.Role;
            entity.ReceivesOperationalEmails = user.ReceivesOperationalEmails;
            entity.RegisteredAt = user.RegisteredAt;
            entity.LastUpdatedAt = DateTime.UtcNow;
            entity.UnsubscribeToken = user.UnsubscribeToken == Guid.Empty
                ? (entity.UnsubscribeToken == Guid.Empty ? Guid.NewGuid() : entity.UnsubscribeToken)
                : user.UnsubscribeToken;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("NotificationUser upserted. UserId: {UserId}, Role: {Role}, ReceivesOperational: {ReceivesOperational}",
            user.UserId, user.Role, user.ReceivesOperationalEmails);
    }

    public async Task UpdateProfileAsync(Guid userId, string email, string firstName, string lastName, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NotificationUsers.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("NotificationUser not found when updating profile. UserId: {UserId}", userId);
            return;
        }

        entity.Email = email;
        entity.FirstName = firstName;
        entity.LastName = lastName;
        entity.LastUpdatedAt = updatedAt;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("NotificationUser profile updated. UserId: {UserId}", userId);
    }

    public async Task<IReadOnlyList<NotificationUser>> GetOperationalContactsAsync(CancellationToken cancellationToken = default)
    {
        var contacts = await _context.NotificationUsers
            .AsNoTracking()
            .Where(x => x.ReceivesOperationalEmails)
            .OrderBy(x => x.FirstName)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Fetched {Count} operational contacts for notifications.", contacts.Count);
        return contacts;
    }

    public async Task<bool> UpdateOperationalPreferenceAsync(Guid userId, bool receivesOperationalEmails, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NotificationUsers.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("NotificationUser not found when toggling operational preference. UserId: {UserId}", userId);
            return false;
        }

        entity.ReceivesOperationalEmails = receivesOperationalEmails;
        entity.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("NotificationUser operational preference updated. UserId: {UserId}, Enabled: {Enabled}", userId, receivesOperationalEmails);
        return true;
    }

    public async Task<NotificationUser?> GetByUnsubscribeTokenAsync(Guid token, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UnsubscribeToken == token, cancellationToken);
    }

    public async Task RegenerateUnsubscribeTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NotificationUsers.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (entity == null)
        {
            _logger.LogWarning("NotificationUser not found when regenerating unsubscribe token. UserId: {UserId}", userId);
            return;
        }

        entity.UnsubscribeToken = Guid.NewGuid();
        entity.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("NotificationUser unsubscribe token regenerated. UserId: {UserId}", userId);
    }
}
