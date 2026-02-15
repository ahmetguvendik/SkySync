using Microsoft.EntityFrameworkCore;
using SkySync.Services.Identity.Domain.Constants;
using SkySync.Services.Identity.Domain.Entities;
using SkySync.Shared.OutboxTable;

namespace SkySync.Services.Identity.Persistence.Contexts;

public class IdentityServiceDbContext : DbContext
{
    public IdentityServiceDbContext(DbContextOptions<IdentityServiceDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(r => r.Name).IsUnique();

            var seedTime = new DateTime(2025, 02, 15, 0, 0, 0, DateTimeKind.Utc);
            entity.HasData(
                new Role
                {
                    Id = RoleConstants.AdminRoleId,
                    Name = RoleConstants.Admin,
                    CreatedTime = seedTime,
                    ModifiedTime = seedTime,
                    IsDeleted = false
                },
                new Role
                {
                    Id = RoleConstants.UserRoleId,
                    Name = RoleConstants.User,
                    CreatedTime = seedTime,
                    ModifiedTime = seedTime,
                    IsDeleted = false
                });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(255);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.LastName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.RoleId).IsRequired().HasDefaultValue(RoleConstants.UserRoleId);
            entity.Property(u => u.IsEmailConfirmed).HasDefaultValue(false);

            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => new { u.Email, u.IsDeleted });

            entity.HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).IsRequired().HasMaxLength(255);
            entity.Property(o => o.Content).IsRequired();
            entity.Property(o => o.OccurredOn).IsRequired();
            entity.Property(o => o.RetryCount).IsRequired().HasDefaultValue(0);
            entity.Property(o => o.IsFailed).IsRequired().HasDefaultValue(false);
            entity.HasIndex(o => o.ProcessedOn);
            entity.HasIndex(o => new { o.IsFailed, o.ProcessedOn });
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Token).IsRequired().HasMaxLength(200);
            entity.Property(p => p.ExpiresAt).IsRequired();
            entity.Property(p => p.IsUsed).IsRequired().HasDefaultValue(false);
            entity.Property(p => p.UsedAt).IsRequired(false);

            entity.HasIndex(p => p.Token).IsUnique();
            entity.HasIndex(p => new { p.UserId, p.IsUsed });

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ExpiresAt).IsRequired();
            entity.Property(e => e.IsUsed).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.UsedAt).IsRequired(false);

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => new { e.UserId, e.IsUsed });

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }
}
