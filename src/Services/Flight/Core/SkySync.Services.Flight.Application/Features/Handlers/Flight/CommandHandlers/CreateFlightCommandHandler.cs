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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateFlightCommandHandler> _logger;
    private readonly ICacheService _cacheService;

    // Flights listesi cache key'i - GetAllFlightsQueryHandler ile aynı olmalı
    // Orada: private const string CacheKey = "flights:all";
    private const string FlightsCacheKey = "flights:all";

    public CreateFlightCommandHandler(
        IOutboxRepository outboxRepository,
        IGenericRepository<FlightEntity> flightRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        ILogger<CreateFlightCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _flightRepository = flightRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<CreateFlightCommandResponse> Handle(CreateFlightCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Transaction başlat
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var flightId = Guid.NewGuid();

            // 1. ADIM: Uçuş Entity'sini Oluştur ve Kaydet
            var seats = GenerateSeats(flightId, request.BasePrice);
            var flight = new FlightEntity
            {
                Id = flightId,
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
    /// Uçuş için otomatik koltuklar oluşturur.
    /// Standart uçak yapısı: 30 satır x 6 sütun (A, B, C, D, E, F) = 180 koltuk
    /// 
    /// PREMIUM FİYATLANDIRMA:
    /// - İlk 10 sıra (1A-10F): Premium Class - BasePrice * 1.5
    /// - Son 20 sıra (11A-30F): Economy Class - BasePrice
    /// </summary>
    private static List<SeatEntity> GenerateSeats(Guid flightId, decimal basePrice)
    {
        var seats = new List<SeatEntity>();
        var seatLetters = new[] { "A", "B", "C", "D", "E", "F" };
        const int totalRows = 30;
        const int premiumRows = 10; // İlk 10 sıra Premium
        const decimal premiumMultiplier = 1.5m; // %50 daha pahalı
        var now = DateTime.UtcNow;

        for (int row = 1; row <= totalRows; row++)
        {
            // İlk 10 sıra Premium, geri kalanı Economy
            var seatPrice = row <= premiumRows 
                ? basePrice * premiumMultiplier  // Premium Class
                : basePrice;                      // Economy Class

            foreach (var letter in seatLetters)
            {
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
            }
        }

        return seats;
    }
}