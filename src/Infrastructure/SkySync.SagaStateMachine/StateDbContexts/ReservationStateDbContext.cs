using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using SkySync.SagaStateMachine.StateInstances;
using SkySync.SagaStateMachine.StateMaps;

namespace SkySync.SagaStateMachine.StateDbContexts;

/// <summary>
/// Saga State Machine için DbContext
/// ReservationState'leri saklamak için kullanılır
/// </summary>
public class ReservationStateDbContext : SagaDbContext
{
    public ReservationStateDbContext(DbContextOptions<ReservationStateDbContext> options)
        : base(options)
    {
    }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new ReservationStateMap(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ReservationState Configuration
        modelBuilder.Entity<ReservationState>(entity =>
        {
            entity.HasKey(x => x.CorrelationId);
            entity.Property(x => x.CurrentState).HasMaxLength(64);
            entity.Property(x => x.SeatNumber).HasMaxLength(10);
            entity.Property(x => x.PassengerName).HasMaxLength(100);
            entity.Property(x => x.PassengerSurname).HasMaxLength(100);
            entity.Property(x => x.PassengerEmail).HasMaxLength(255);
            entity.Property(x => x.Price).HasColumnType("decimal(18,2)");
        });
    }
}
