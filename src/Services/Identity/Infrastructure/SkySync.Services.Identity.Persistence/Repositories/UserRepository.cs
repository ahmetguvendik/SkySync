using Microsoft.EntityFrameworkCore;
using SkySync.Services.Identity.Application.Interfaces;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Services.Identity.Persistence.Contexts;

namespace SkySync.Services.Identity.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityServiceDbContext _context;

    public UserRepository(IdentityServiceDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Users, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => !u.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(u => u.CreatedTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (users, totalCount);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        return user;
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AnyAsync(u => u.Email == email && !u.IsDeleted, cancellationToken);
    }

    public async Task<bool> ExistsByEmailExceptIdAsync(string email, Guid excludeUserId, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .AnyAsync(u => u.Email == email && u.Id != excludeUserId && !u.IsDeleted, cancellationToken);
    }

    public async Task<bool> UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            return false;
        }

        user.PasswordHash = passwordHash;
        user.ModifiedTime = DateTime.UtcNow;
        return true;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        user.ModifiedTime = DateTime.UtcNow;
        _context.Users.Update(user);
        return Task.CompletedTask;
    }
}
