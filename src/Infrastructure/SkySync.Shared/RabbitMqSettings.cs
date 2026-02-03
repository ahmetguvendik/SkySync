namespace SkySync.Shared;

public static class RabbitMqSettings
{
    // Saga State Machine Queue
    public const string ReservationSagaQueue = "reservation-saga-queue";

    // Flight Service Queues
    public const string FlightReserveSeatQueue = "flight-reserve-seat-queue";
    public const string FlightReleaseSeatQueue = "flight-release-seat-queue";

    // Payment Service Queues
    public const string PaymentProcessQueue = "payment-process-queue";

    // Notification Service Queues
    public const string NotificationReservationConfirmedQueue = "notification-confirmed-queue";
    public const string NotificationFlightCreatedQueue = "notification-flight-created-queue";

    // Reservation Service – Flight read model (FlightCreatedEvent consumer)
    public const string ReservationFlightCreatedQueue = "reservation-flight-created-queue";
}
