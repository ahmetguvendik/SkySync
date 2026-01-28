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
}
