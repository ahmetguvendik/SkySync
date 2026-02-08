using Microsoft.EntityFrameworkCore;
using SkySync.Services.Payment.Domain.Entities;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Payment.Persistence.Contexts;

public class PaymentServiceDbContext : DbContext
{
    public PaymentServiceDbContext(DbContextOptions<PaymentServiceDbContext> options) : base(options)
    {
    }

    public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    
    /// <summary>
    /// Inbox Messages - Duplicate payment prevention (Shared'dan)
    /// </summary>
    public DbSet<InboxMessage> InboxMessages { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status)
                .HasConversion<string>() // PaymentStatus enum'u string olarak sakla
                .IsRequired()
                .HasMaxLength(20);
            entity.HasIndex(e => e.ReservationId);
            entity.HasIndex(e => e.CorrelationId);
        });

        // InboxMessage Configuration - Idempotency için kritik!
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

            // Indexes
            entity.HasIndex(i => i.EventType);
            entity.HasIndex(i => i.ProcessedAt);
            entity.HasIndex(i => new { i.EventType, i.ProcessedAt });
            entity.HasIndex(i => i.Status);
            
            // ✅ KRITIK: Business Key unique constraint - Duplicate payment prevention!
            entity.HasIndex(i => new { i.EventType, i.BusinessKey })
                .IsUnique();
        });

    }
}
