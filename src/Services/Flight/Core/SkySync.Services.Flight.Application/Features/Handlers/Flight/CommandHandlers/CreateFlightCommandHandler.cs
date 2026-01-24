using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;

namespace SkySync.Services.Flight.Application.Features.Handlers.Flight.CommandHandlers;

public class CreateFlightCommandHandler : IRequestHandler<CreateFlightCommandRequest, CreateFlightCommandResponse>
{
    private readonly IGenericRepository<OutboxMessage> _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateFlightCommandHandler> _logger;

    public CreateFlightCommandHandler(
        IGenericRepository<OutboxMessage> outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateFlightCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreateFlightCommandResponse> Handle(CreateFlightCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Transaction başlat
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var flightId = Guid.NewGuid();

            // Outbox pattern: Event oluştur
            var flightCreatedEvent = new FlightCreatedEvent
            {
                FlightId = flightId,
                FlightNumber = request.FlightNumber,
                Departure = request.Departure,
                Destination = request.Destination,
                DepartureTime = request.DepartureTime,
                ArrivalTime = request.ArrivalTime,
                BasePrice = request.BasePrice,
                Status = request.Status.ToString(),
                CreatedAt = DateTime.UtcNow
            };

            // Event'i JSON'a çevir
            var eventContent = JsonSerializer.Serialize(flightCreatedEvent);

            // OutboxMessage oluştur
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = nameof(FlightCreatedEvent),
                Content = eventContent,
                OccurredOn = DateTime.UtcNow,
                ProcessedOn = null,
                Error = null
            };

            // Sadece OutboxMessage'i kaydet 
            await _outboxRepository.CreateAsync(outboxMessage, cancellationToken);

            // Transaction'ı commit et
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Flight creation event added to outbox. FlightId: {FlightId}, FlightNumber: {FlightNumber}",
                flightId, request.FlightNumber);

            return new CreateFlightCommandResponse
            {
                FlightId = flightId,
                FlightNumber = request.FlightNumber,
                IsSuccess = true,
                Message = "Flight creation event added to outbox successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while adding flight creation event to outbox. FlightNumber: {FlightNumber}",
                request.FlightNumber);

            // Transaction'ı rollback et
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);

            return new CreateFlightCommandResponse
            {
                FlightId = Guid.Empty,
                FlightNumber = request.FlightNumber,
                IsSuccess = false,
                Message = $"Error occurred while adding flight creation event to outbox: {ex.Message}"
            };
        }
    }
}