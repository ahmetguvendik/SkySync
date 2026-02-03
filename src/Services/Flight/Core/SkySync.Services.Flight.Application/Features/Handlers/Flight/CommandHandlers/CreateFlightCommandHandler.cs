using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using SkySync.Shared.Events;
using SkySync.Shared.OutboxTable;
using FlightEntity = SkySync.Services.Flight.Domain.Entities.Flight;
using SeatEntity = SkySync.Services.Flight.Domain.Entities.Seat;

namespace SkySync.Services.Flight.Application.Features.Handlers.Flight.CommandHandlers;

public class CreateFlightCommandHandler : IRequestHandler<CreateFlightCommandRequest, CreateFlightCommandResponse>
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IGenericRepository<FlightEntity> _flightRepository;
    private readonly IAircraftRepository _aircraftRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateFlightCommandHandler> _logger;
    private readonly ICacheService _cacheService;

    private const string FlightsCacheKey = "flights:all";

    public CreateFlightCommandHandler(
        IOutboxRepository outboxRepository,
        IGenericRepository<FlightEntity> flightRepository,
        IAircraftRepository aircraftRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<CreateFlightCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _flightRepository = flightRepository;
        _aircraftRepository = aircraftRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<CreateFlightCommandResponse> Handle(CreateFlightCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var aircraft = await _aircraftRepository.GetByIdAsync(request.AircraftId, cancellationToken);
            if (aircraft == null)
            {
                return new CreateFlightCommandResponse
                {
                    FlightId = Guid.Empty,
                    FlightNumber = request.FlightNumber,
                    IsSuccess = false,
                    Message = $"Aircraft not found. AircraftId: {request.AircraftId}"
                };
            }

            // Transaction başlat
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var flightId = Guid.NewGuid();

            // 1. ADIM: Seçilen uçağın koltuk sayısına göre koltuklar oluştur
            var seats = GenerateSeats(flightId, request.BasePrice, aircraft.SeatCount);
            var flight = new FlightEntity
            {
                Id = flightId,
                AircraftId = request.AircraftId,
                FlightNumber = request.FlightNumber,
                Departure = request.Departure,
                Destination = request.Destination,
                DepartureTime = request.DepartureTime,
                ArrivalTime = request.ArrivalTime,
                BasePrice = request.BasePrice,
                Status = request.Status,
                Seats = seats
            };

            // Seats'ların Flight navigation property'sini set et
            foreach (var seat in seats)
            {
                seat.Flight = flight;
            }

            await _flightRepository.CreateAsync(flight, cancellationToken);

            // 2. ADIM: Outbox Mesajını Oluştur
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

            await _outboxRepository.CreateAsync(outboxMessage, cancellationToken);

            // 3. ADIM: Hepsini Tek Transaction'da Bitir
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Flight created successfully. FlightId: {FlightId}, FlightNumber: {FlightNumber}, Seats: {SeatCount}",
                flightId, request.FlightNumber, seats.Count);

            // 4. ADIM: Cache Invalidasyonu (Cache-Aside Pattern için kritik!)
            try
            {
                await _cacheService.RemoveAsync(FlightsCacheKey, cancellationToken);
                _logger.LogInformation("Flight cache invalidated for key {Key}", FlightsCacheKey);
            }
            catch (Exception cacheEx)
            {
                // Cache hatası uçuş oluşturmayı bozmamalı, sadece logla
                _logger.LogWarning(cacheEx,
                    "Failed to invalidate flight cache for key {Key}", FlightsCacheKey);
            }

            return new CreateFlightCommandResponse
            {
                FlightId = flightId,
                FlightNumber = request.FlightNumber,
                IsSuccess = true,
                Message = "Flight created successfully"
            };
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" Inner Exception: {ex.InnerException.Message}";
            }

            _logger.LogError(ex, "Error occurred while creating flight. FlightNumber: {FlightNumber}, Error: {Error}",
                request.FlightNumber, errorMessage);

            // Transaction'ı rollback et
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);

            return new CreateFlightCommandResponse
            {
                FlightId = Guid.Empty,
                FlightNumber = request.FlightNumber,
                IsSuccess = false,
                Message = $"Error occurred while creating flight: {errorMessage}"
            };
        }
    }

    /// <summary>
    /// Uçuş için seçilen uçağın koltuk sayısına göre koltuklar oluşturur.
    /// Sıra x sütun (1A, 1B, ... 1F, 2A, ...) – sütun sayısı 6 (A-F).
    /// İlk 10 sıra Premium (BasePrice * 1.5), geri kalan Economy.
    /// </summary>
    private static List<SeatEntity> GenerateSeats(Guid flightId, decimal basePrice, int seatCount)
    {
        var seats = new List<SeatEntity>();
        var seatLetters = new[] { "A", "B", "C", "D", "E", "F" };
        const int seatsPerRow = 6;
        const int premiumRows = 10;
        const decimal premiumMultiplier = 1.5m;
        var now = DateTime.UtcNow;

        int generated = 0;
        int row = 1;
        while (generated < seatCount)
        {
            var seatPrice = row <= premiumRows
                ? basePrice * premiumMultiplier
                : basePrice;

            foreach (var letter in seatLetters)
            {
                if (generated >= seatCount) break;

                var seat = new SeatEntity
                {
                    Id = Guid.NewGuid(),
                    FlightId = flightId,
                    SeatNumber = $"{row}{letter}",
                    Price = seatPrice,
                    IsReserved = false,
                    UserId = null,
                    CreatedTime = now,
                    ModifiedTime = now,
                    IsDeleted = false
                };
                seats.Add(seat);
                generated++;
            }
            row++;
        }

        return seats;
    }
}