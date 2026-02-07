using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Persistence.Contexts;
using SkySync.Shared.Commands;
using SkySync.Shared.InboxPattern;

namespace SkySync.Services.Flight.Persistence.Consumers;

/// <summary>
/// Release Seat Consumer - Inbox Pattern for idempotency
/// Prevents duplicate seat releases during compensating transactions
/// </summary>
public class ReleaseSeatCommandConsumer : IConsumer<ReleaseSeatCommand>
{
    private readonly FlightServiceDbContext _context;
    private readonly IInboxService _inboxService;
    private readonly ILogger<ReleaseSeatCommandConsumer> _logger;

    public ReleaseSeatCommandConsumer(
        FlightServiceDbContext context,
        IInboxService inboxService,
        ILogger<ReleaseSeatCommandConsumer> logger)
    {
        _context = context;
        _inboxService = inboxService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReleaseSeatCommand> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _logger.LogInformation(
            "Seat release command received. MessageId: {MessageId}, FlightId: {FlightId}, SeatNumber: {SeatNumber}, CorrelationId: {CorrelationId}",
            messageId, message.FlightId, message.SeatNumber, message.CorrelationId);

        // INBOX PATTERN - Idempotency Check
        var businessKey = $"{message.FlightId}:{message.SeatNumber}:{message.CorrelationId}";
        var markedSuccess = await _inboxService.MarkAsProcessedAsync(
            messageId,
            nameof(ReleaseSeatCommand),
            businessKey,
            JsonSerializer.Serialize(message));

        if (!markedSuccess)
        {
            // Duplicate command blocked!
            _logger.LogWarning(
                "Duplicate seat release command blocked. FlightId: {FlightId}, SeatNumber: {SeatNumber}, MessageId: {MessageId}",
                message.FlightId, message.SeatNumber, messageId);
            return;
        }

        _logger.LogInformation(
            "Command locked for processing. FlightId: {FlightId}, SeatNumber: {SeatNumber}",
            message.FlightId, message.SeatNumber);

        try
        {
            var seat = await _context.Seats.FirstOrDefaultAsync(x => 
                x.FlightId == message.FlightId && 
                x.SeatNumber == message.SeatNumber);

            if (seat != null)
            {
                seat.IsReserved = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Seat released successfully. FlightId: {FlightId}, SeatNumber: {SeatNumber}, MessageId: {MessageId}",
                    message.FlightId, message.SeatNumber, messageId);
            }
            else
            {
                _logger.LogWarning(
                    "Seat not found for release. FlightId: {FlightId}, SeatNumber: {SeatNumber}",
                    message.FlightId, message.SeatNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error occurred while releasing seat. FlightId: {FlightId}, SeatNumber: {SeatNumber}, MessageId: {MessageId}",
                message.FlightId, message.SeatNumber, messageId);

            await _inboxService.MarkAsFailedAsync(
                messageId,
                nameof(ReleaseSeatCommand),
                businessKey,
                ex.Message,
                JsonSerializer.Serialize(message));

            throw;
        }
    }
}
