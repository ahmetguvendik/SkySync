using Microsoft.EntityFrameworkCore;
using SkySync.Services.Notification.Domain.Entities;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Notification.Persistence.Contexts;

/// <summary>
/// Notification Service DbContext - Inbox Pattern için
/// </summary>
public class NotificationServiceDbContext : DbContext
{
    public NotificationServiceDbContext(DbContextOptions<NotificationServiceDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Inbox Messages - Duplicate event handling için (Shared'dan)
    /// </summary>
    public DbSet<InboxMessage> InboxMessages { get; set; }

    /// <summary>
    /// Identity servisinden replike edilen kullanıcılar
    /// </summary>
    public DbSet<NotificationUser> NotificationUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // InboxMessage Configuration
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(i => i.MessageId);

            entity.Property(i => i.BusinessKey)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(i => i.EventType)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(i => i.ProcessedAt)
                .IsRequired();

            entity.Property(i => i.Status)
                .HasConversion<string>()
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(i => i.EventPayload)
                .HasColumnType("text");

            entity.Property(i => i.ErrorMessage)
                .HasMaxLength(1000);

            entity.Property(i => i.RetryCount)
                .IsRequired()
                .HasDefaultValue(0);

            // Indexes for performance
            entity.HasIndex(i => i.EventType);
            entity.HasIndex(i => i.ProcessedAt);
            entity.HasIndex(i => new { i.EventType, i.ProcessedAt });
            entity.HasIndex(i => i.Status);

            // ✅ Business Key index - Idempotency için kritik!
            entity.HasIndex(i => new { i.EventType, i.BusinessKey })
                .IsUnique();
        });

        modelBuilder.Entity<NotificationUser>(entity =>
        {
            entity.HasKey(x => x.UserId);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(x => x.FirstName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.LastName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Role)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.ReceivesOperationalEmails)
                .HasDefaultValue(false);

            entity.Property(x => x.UnsubscribeToken)
                .IsRequired();

            entity.HasIndex(x => x.ReceivesOperationalEmails);
            entity.HasIndex(x => x.UnsubscribeToken)
                .IsUnique();
        });
    }
}
