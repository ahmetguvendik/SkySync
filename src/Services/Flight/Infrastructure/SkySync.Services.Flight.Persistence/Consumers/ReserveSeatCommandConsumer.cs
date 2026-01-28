using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Persistence.Contexts;
using SkySync.Shared.Commands;
using SkySync.Shared.Events;

namespace SkySync.Services.Flight.Persistence.Consumers;

/// <summary>
/// Seat Reservation Consumer - Distributed Lock ile Race Condition koruması
/// Senior Level: Overbooking önleme mekanizması
/// </summary>
public class ReserveSeatCommandConsumer : IConsumer<ReserveSeatCommand>
{
    private readonly FlightServiceDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<ReserveSeatCommandConsumer> _logger;
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(5);

    public ReserveSeatCommandConsumer(
        FlightServiceDbContext context,
        ICacheService cacheService,
        ILogger<ReserveSeatCommandConsumer> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReserveSeatCommand> context)
    {
        var message = context.Message;
        
        // 🔒 Distributed Lock - Race Condition önleme
        // Her FlightId + SeatNumber kombinasyonu için benzersiz lock key
        var lockKey = $"seat:{message.FlightId}:{message.SeatNumber}";
        
        _logger.LogInformation(
            "Attempting to acquire lock for seat reservation. FlightId: {FlightId}, SeatNumber: {SeatNumber}, CorrelationId: {CorrelationId}",
            message.FlightId, message.SeatNumber, message.CorrelationId);

        var distributedLock = await _cacheService.AcquireLockAsync(lockKey, LockExpiration);

        if (distributedLock == null || !distributedLock.IsAcquired)
        {
            // Lock alınamazsa, başka bir işlem devam ediyor demektir
            _logger.LogWarning(
                "Failed to acquire lock for seat reservation. Seat might be under reservation by another process. FlightId: {FlightId}, SeatNumber: {SeatNumber}",
                message.FlightId, message.SeatNumber);

            await context.Publish(new FlightReservationFailedEvent
            {
                CorrelationId = message.CorrelationId,
                FlightId = message.FlightId,
                SeatNumber = message.SeatNumber,
                ErrorMessage = "Koltuk şu anda başka bir işlem tarafından rezerve ediliyor. Lütfen tekrar deneyin.",
                FailedAt = DateTime.UtcNow
            });
            return;
        }

        try
        {
            _logger.LogInformation(
                "Lock acquired successfully. Processing seat reservation. FlightId: {FlightId}, SeatNumber: {SeatNumber}",
                message.FlightId, message.SeatNumber);

            // Lock alındı, güvenli şekilde kontrol ve rezervasyon işlemi
            var seat = await _context.Seats.FirstOrDefaultAsync(x => 
                x.FlightId == message.FlightId && 
                x.SeatNumber == message.SeatNumber);

            if (seat == null || seat.IsReserved)
            {
                _logger.LogWarning(
                    "Seat not found or already reserved. FlightId: {FlightId}, SeatNumber: {SeatNumber}, IsReserved: {IsReserved}",
                    message.FlightId, message.SeatNumber, seat?.IsReserved);

                await context.Publish(new FlightReservationFailedEvent
                {
                    CorrelationId = message.CorrelationId,
                    FlightId = message.FlightId,
                    SeatNumber = message.SeatNumber,
                    ErrorMessage = "Koltuk bulunamadı veya zaten rezerve edilmiş.",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            // Koltuğu rezerve et
            seat.IsReserved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Seat reserved successfully. FlightId: {FlightId}, SeatNumber: {SeatNumber}, CorrelationId: {CorrelationId}",
                message.FlightId, message.SeatNumber, message.CorrelationId);

            await context.Publish(new FlightReservedEvent
            {
                CorrelationId = message.CorrelationId,
                FlightId = message.FlightId,
                SeatNumber = message.SeatNumber,
                IsSuccess = true,
                ReservedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error occurred while processing seat reservation. FlightId: {FlightId}, SeatNumber: {SeatNumber}",
                message.FlightId, message.SeatNumber);

            await context.Publish(new FlightReservationFailedEvent
            {
                CorrelationId = message.CorrelationId,
                FlightId = message.FlightId,
                SeatNumber = message.SeatNumber,
                ErrorMessage = $"Koltuk rezervasyonu sırasında hata oluştu: {ex.Message}",
                FailedAt = DateTime.UtcNow
            });
        }
        finally
        {
            // Lock'u her durumda serbest bırak
            if (distributedLock != null && distributedLock.IsAcquired)
            {
                await _cacheService.ReleaseLockAsync(distributedLock);
                _logger.LogInformation("Lock released for seat: {FlightId}:{SeatNumber}", message.FlightId, message.SeatNumber);
            }
        }
    }
}
