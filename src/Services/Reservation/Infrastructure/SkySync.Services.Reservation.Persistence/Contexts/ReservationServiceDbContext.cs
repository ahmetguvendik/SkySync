using Microsoft.EntityFrameworkCore;
using SkySync.Shared.InboxPattern;
using SkySync.Shared.OutboxTable;

namespace SkySync.Services.Reservation.Persistence.Contexts;

public class ReservationServiceDbContext : DbContext
{
    public ReservationServiceDbContext(DbContextOptions<ReservationServiceDbContext> options) : base(options)
    {
    }

    public DbSet<SkySync.Services.Reservation.Domain.Entities.Reservation> Reservations { get; set; }
    public DbSet<SkySync.Services.Reservation.Domain.Entities.FlightSummary> FlightSummaries { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<InboxMessage> InboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Reservation Configuration
        modelBuilder.Entity<SkySync.Services.Reservation.Domain.Entities.Reservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.SeatNumber).IsRequired().HasMaxLength(10);
            entity.Property(r => r.Price).HasColumnType("decimal(18,2)");
            entity.Property(r => r.PassengerName).IsRequired().HasMaxLength(100);
            entity.Property(r => r.PassengerSurname).IsRequired().HasMaxLength(100);
            entity.Property(r => r.PassengerEmail).IsRequired().HasMaxLength(255);
            entity.Property(r => r.Status).HasConversion<int>(); // Enum'u integer olarak sakla
            entity.Property(r => r.ReminderSentAt).IsRequired(false);

            // Indexes
            entity.HasIndex(r => r.PassengerEmail);
            entity.HasIndex(r => r.FlightId);
            entity.HasIndex(r => r.CorrelationId); // Saga takibi için
            entity.HasIndex(r => new { r.PassengerEmail, r.IsDeleted });
        });

        // FlightSummary read model (FlightCreated/FlightUpdated event'leri ile beslenir)
        modelBuilder.Entity<SkySync.Services.Reservation.Domain.Entities.FlightSummary>(entity =>
        {
            entity.HasKey(f => f.FlightId);
            entity.Property(f => f.FlightNumber).IsRequired().HasMaxLength(50);
            entity.Property(f => f.Departure).HasMaxLength(100);
            entity.Property(f => f.Destination).HasMaxLength(100);
            entity.Property(f => f.DepartureTime).IsRequired();
            entity.Property(f => f.ArrivalTime).IsRequired();
            entity.Property(f => f.UpdatedAt).IsRequired();
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

        // InboxMessage Configuration - Idempotency for status consumers
        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(i => i.MessageId);
            entity.Property(i => i.BusinessKey).IsRequired().HasMaxLength(255);
            entity.Property(i => i.EventType).IsRequired().HasMaxLength(255);
            entity.Property(i => i.ProcessedAt).IsRequired();
            entity.Property(i => i.Status).HasConversion<string>().IsRequired().HasMaxLength(50);
            entity.Property(i => i.EventPayload).HasColumnType("text");
            entity.Property(i => i.ErrorMessage).HasMaxLength(1000);
            entity.Property(i => i.RetryCount).IsRequired().HasDefaultValue(0);
            entity.HasIndex(i => i.EventType);
            entity.HasIndex(i => i.ProcessedAt);
            entity.HasIndex(i => new { i.EventType, i.ProcessedAt });
            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => new { i.EventType, i.BusinessKey }).IsUnique();
        });
    }
}
