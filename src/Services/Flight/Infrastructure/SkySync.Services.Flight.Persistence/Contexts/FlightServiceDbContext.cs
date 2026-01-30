using Microsoft.EntityFrameworkCore;
using SkySync.Shared.InboxPattern;
using SkySync.Shared.OutboxTable;

namespace SkySync.Services.Flight.Persistence.Contexts;

public class FlightServiceDbContext : DbContext
{
    public FlightServiceDbContext(DbContextOptions<FlightServiceDbContext> options) : base(options)
    {
        
    }

    public DbSet<SkySync.Services.Flight.Domain.Entities.Flight> Flights { get; set; }
    public DbSet<SkySync.Services.Flight.Domain.Entities.Seat> Seats { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    
    /// <summary>
    /// Inbox Messages - Duplicate seat reservation prevention (Shared'dan)
    /// </summary>
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Flight Configuration
        modelBuilder.Entity<SkySync.Services.Flight.Domain.Entities.Flight>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FlightNumber).IsRequired().HasMaxLength(50);
            entity.Property(f => f.Departure).IsRequired().HasMaxLength(100);
            entity.Property(f => f.Destination).IsRequired().HasMaxLength(100);
            entity.Property(f => f.BasePrice).HasColumnType("decimal(18,2)");
            entity.Property(f => f.Status).HasConversion<int>();
            
            // Relationships
            entity.HasMany(f => f.Seats)
                .WithOne(s => s.Flight)
                .HasForeignKey(s => s.FlightId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seat Configuration
        modelBuilder.Entity<SkySync.Services.Flight.Domain.Entities.Seat>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SeatNumber).IsRequired().HasMaxLength(10);
            entity.Property(s => s.Price).HasColumnType("decimal(18,2)");
            entity.HasIndex(s => new { s.FlightId, s.SeatNumber }).IsUnique();
        });

        // OutboxMessage Configuration
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
            
            // ✅ KRITIK: Business Key unique constraint - Duplicate seat reservation prevention!
            entity.HasIndex(i => new { i.EventType, i.BusinessKey })
                .IsUnique();
        });
    }
}