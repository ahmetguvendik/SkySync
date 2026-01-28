using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Shared;
using SkySync.Shared.Commands;
using SkySync.Shared.Events;

namespace SkySync.SagaStateMachine.StateMachines;

/// <summary>
/// Reservation Saga State Machine
/// Senior Level: Saga Pattern - Distributed Transaction Management
/// 
/// Akış:
/// 1. ReservationStarted -> Flight Service'e "Koltuk ayır" de
/// 2. FlightReserved -> Payment Service'e "Parayı çek" de
/// 3. PaymentCompleted -> Rezervasyonu Confirmed yap ve Notification'a haber ver
/// 4. PaymentFailed -> Flight Service'e "Koltuğu geri aç" (Compensate) de ve rezervasyonu Failed yap
/// </summary>
public class ReservationStateMachine : MassTransitStateMachine<StateInstances.ReservationState>
{
    private readonly ILogger<ReservationStateMachine> _logger;

    public ReservationStateMachine(ILogger<ReservationStateMachine> logger)
    {
        _logger = logger;

        // State Definitions
        InstanceState(x => x.CurrentState);

        // Event Definitions with CorrelationId Mapping
        // CRITICAL: CorrelateById tells MassTransit which saga instance to use based on CorrelationId
        Event(() => ReservationStarted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FlightReserved, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FlightReservationFailed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentCompleted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentFailed, x => x.CorrelateById(context => context.Message.CorrelationId));

        // Initial State
        Initially(
            When(ReservationStarted)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.CorrelationId = context.Message.CorrelationId;
                    state.ReservationId = context.Message.ReservationId;
                    state.FlightId = context.Message.FlightId;
                    state.SeatNumber = context.Message.SeatNumber;
                    state.Price = context.Message.Price;
                    state.PassengerName = context.Message.PassengerName;
                    state.PassengerSurname = context.Message.PassengerSurname;
                    state.PassengerEmail = context.Message.PassengerEmail;
                    state.CreatedAt = context.Message.CreatedAt;
                    // CurrentState MassTransit tarafından otomatik set edilir
                })
                .Send(new Uri($"queue:{RabbitMqSettings.FlightReserveSeatQueue}"), context => new ReserveSeatCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    FlightId = context.Saga.FlightId,
                    SeatNumber = context.Saga.SeatNumber
                })
                .TransitionTo(AwaitingFlightReservation));

        // Awaiting Flight Reservation
        During(AwaitingFlightReservation,
            When(FlightReserved)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.FlightReservedAt = context.Message.ReservedAt;
                    // CurrentState MassTransit tarafından otomatik set edilir
                })
                .Publish(context => new ProcessPaymentCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    ReservationId = context.Saga.ReservationId,
                    Amount = context.Saga.Price,
                    PassengerEmail = context.Saga.PassengerEmail
                })
                .TransitionTo(AwaitingPayment),

            When(FlightReservationFailed)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.ErrorMessage = context.Message.ErrorMessage;
                    // CurrentState MassTransit tarafından otomatik set edilir
                })
                .Finalize());

        // Awaiting Payment
        During(AwaitingPayment,
            When(PaymentCompleted)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.PaymentCompletedAt = context.Message.CompletedAt;
                    state.CompletedAt = DateTime.UtcNow;
                    // CurrentState MassTransit tarafından otomatik set edilir
                })
                .Publish(context => new ReservationConfirmedEvent
                {
                    CorrelationId = context.Saga.CorrelationId,
                    ReservationId = context.Saga.ReservationId,
                    FlightId = context.Saga.FlightId,
                    SeatNumber = context.Saga.SeatNumber,
                    PassengerEmail = context.Saga.PassengerEmail,
                    PassengerName = context.Saga.PassengerName,
                    PassengerSurname = context.Saga.PassengerSurname,
                    Price = context.Saga.Price,
                    ConfirmedAt = DateTime.UtcNow
                })
                .Finalize(),

            When(PaymentFailed)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.ErrorMessage = context.Message.ErrorMessage;
                    // CurrentState MassTransit tarafından otomatik set edilir
                })
                .Publish(context => new ReleaseSeatCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    FlightId = context.Saga.FlightId,
                    SeatNumber = context.Saga.SeatNumber
                })
                .Finalize());

        // Unhandled events will be logged but not cause errors
        // MassTransit handles unhandled events gracefully
    }

    // States
    public State AwaitingFlightReservation { get; private set; }
    public State AwaitingPayment { get; private set; }

    // Events
    public Event<ReservationStartedEvent> ReservationStarted { get; private set; }
    public Event<FlightReservedEvent> FlightReserved { get; private set; }
    public Event<FlightReservationFailedEvent> FlightReservationFailed { get; private set; }
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; }
    public Event<PaymentFailedEvent> PaymentFailed { get; private set; }
}
