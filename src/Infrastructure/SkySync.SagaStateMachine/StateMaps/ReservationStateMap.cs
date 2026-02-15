using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SkySync.SagaStateMachine.StateInstances;

namespace SkySync.SagaStateMachine.StateMaps;

/// <summary>
/// ReservationState için Entity Framework Core mapping
/// </summary>
public class ReservationStateMap : SagaClassMap<ReservationState>
{
    protected override void Configure(EntityTypeBuilder<ReservationState> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState)
            .HasMaxLength(64);

        entity.Property(x => x.SeatNumber)
            .HasMaxLength(10);

        entity.Property(x => x.PassengerName)
            .HasMaxLength(100);

        entity.Property(x => x.PassengerSurname)
            .HasMaxLength(100);

        entity.Property(x => x.PassengerEmail)
            .HasMaxLength(255);

        entity.Property(x => x.Price)
            .HasColumnType("decimal(18,2)");
    }
}
