using MassTransit;
using Microsoft.Extensions.Logging;
using SkySync.Shared;
using SkySync.Shared.Commands;
using SkySync.Shared.Events;

namespace SkySync.SagaStateMachine.StateMachines;

/// <summary>
/// Reservation Saga State Machine
/// Zombi Rezervasyon önleme: 5 dk ödeme gelmezse timeout → koltuğu sal.
/// </summary>
public class ReservationStateMachine : MassTransitStateMachine<StateInstances.ReservationState>
{
    private const int PaymentTimeoutMinutes = 5;
    private readonly ILogger<ReservationStateMachine> _logger;

    public ReservationStateMachine(ILogger<ReservationStateMachine> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        Event(() => ReservationStarted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FlightReserved, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FlightReservationFailed, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentCompleted, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => PaymentFailed, x => x.CorrelateById(context => context.Message.CorrelationId));

        Schedule(() => PaymentTimeout, instance => instance.TimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromMinutes(PaymentTimeoutMinutes);
            s.Received = r => r.CorrelateById(context => context.Message.CorrelationId);
        });

        // 1. Rezervasyon başlat → Koltuk rezerve et
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
                    _logger.LogInformation("Saga Started: Reserving seat {Seat} for flight {Flight}",
                        state.SeatNumber, state.FlightId);
                })
                .Send(new Uri($"queue:{RabbitMqSettings.FlightReserveSeatQueue}"), context => new ReserveSeatCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    FlightId = context.Saga.FlightId,
                    SeatNumber = context.Saga.SeatNumber
                })
                .TransitionTo(AwaitingFlightReservation));

        // 2. Koltuk sonucunu bekle
        During(AwaitingFlightReservation,
            When(FlightReserved)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.FlightReservedAt = context.Message.ReservedAt;
                    state.FlightNumber = context.Message.FlightNumber;
                    state.Departure = context.Message.Departure;
                    state.Destination = context.Message.Destination;
                    _logger.LogInformation("Seat Reserved. Processing payment for {ResId}", state.ReservationId);
                })
                .Publish(context => new ProcessPaymentCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    ReservationId = context.Saga.ReservationId,
                    Amount = context.Saga.Price,
                    PassengerEmail = context.Saga.PassengerEmail,
                    ValidUntil = DateTime.UtcNow.AddMinutes(PaymentTimeoutMinutes)
                })
                .Schedule(PaymentTimeout, context => new PaymentTimeoutEvent
                {
                    CorrelationId = context.Saga.CorrelationId,
                    ReservationId = context.Saga.ReservationId,
                    Amount = context.Saga.Price,
                    TimeoutAt = DateTime.UtcNow.AddMinutes(PaymentTimeoutMinutes),
                    Reason = "Ödeme 5 dakika içinde tamamlanmadı."
                })
                .TransitionTo(AwaitingPayment),

            When(FlightReservationFailed)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.ErrorMessage = context.Message.ErrorMessage;
                    _logger.LogError("Seat reservation failed: {Error}", context.Message.ErrorMessage);
                })
                .Finalize());

        // 3. Ödeme sonucunu bekle
        During(AwaitingPayment,
            When(PaymentCompleted)
                .Unschedule(PaymentTimeout)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.PaymentCompletedAt = context.Message.CompletedAt;
                    state.CompletedAt = DateTime.UtcNow;
                    _logger.LogInformation("Payment successful for {ResId}. Completing reservation.", state.ReservationId);
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
                    FlightNumber = context.Saga.FlightNumber,
                    Departure = context.Saga.Departure,
                    Destination = context.Saga.Destination,
                    ConfirmedAt = DateTime.UtcNow
                })
                .Finalize(),

            When(PaymentFailed)
                .Unschedule(PaymentTimeout)
                .Then(context =>
                {
                    var state = context.Saga;
                    state.ErrorMessage = context.Message.ErrorMessage;
                    _logger.LogWarning("Payment failed. Releasing seat {Seat}", state.SeatNumber);
                })
                .Send(new Uri($"queue:{RabbitMqSettings.FlightReleaseSeatQueue}"), context => new ReleaseSeatCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    FlightId = context.Saga.FlightId,
                    SeatNumber = context.Saga.SeatNumber
                })
                .Finalize(),

            When(PaymentTimeout.Received)
                .Then(context =>
                    _logger.LogCritical("Payment Timeout! Releasing seat for {ResId}", context.Saga.CorrelationId))
                .Publish(context => new ReservationTimedOutEvent
                {
                    CorrelationId = context.Saga.CorrelationId,
                    ReservationId = context.Saga.ReservationId,
                    Reason = "Ödeme 5 dakika içinde tamamlanmadı.",
                    TimedOutAt = DateTime.UtcNow
                })
                .Send(new Uri($"queue:{RabbitMqSettings.FlightReleaseSeatQueue}"), context => new ReleaseSeatCommand
                {
                    CorrelationId = context.Saga.CorrelationId,
                    FlightId = context.Saga.FlightId,
                    SeatNumber = context.Saga.SeatNumber
                })
                .Finalize());

        SetCompletedWhenFinalized();
    }

    public Schedule<StateInstances.ReservationState, PaymentTimeoutEvent> PaymentTimeout { get; private set; }
    public State AwaitingFlightReservation { get; private set; }
    public State AwaitingPayment { get; private set; }

    public Event<ReservationStartedEvent> ReservationStarted { get; private set; }
    public Event<FlightReservedEvent> FlightReserved { get; private set; }
    public Event<FlightReservationFailedEvent> FlightReservationFailed { get; private set; }
    public Event<PaymentCompletedEvent> PaymentCompleted { get; private set; }
    public Event<PaymentFailedEvent> PaymentFailed { get; private set; }
}
