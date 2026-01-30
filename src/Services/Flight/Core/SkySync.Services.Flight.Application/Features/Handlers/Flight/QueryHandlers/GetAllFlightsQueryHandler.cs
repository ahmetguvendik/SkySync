using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.DTOs;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using FlightEntity = SkySync.Services.Flight.Domain.Entities.Flight;

namespace SkySync.Services.Flight.Application.Features.Handlers.Flight.QueryHandlers;

/// <summary>
/// CQRS Query Handler - Cache Aside Pattern ile uçuşları getir
/// Senior Level: Cache-Aside Pattern, Distributed Locking, Fail-Safe
/// </summary>
public class GetAllFlightsQueryHandler : IRequestHandler<GetAllFlightsQueryRequest, GetAllFlightsQueryResponse>
{
    private readonly IGenericRepository<FlightEntity> _flightRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<GetAllFlightsQueryHandler> _logger;
    
    private const string CacheKey = "flights:all";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(10); // Lock için maksimum süre

    public GetAllFlightsQueryHandler(
        IGenericRepository<FlightEntity> flightRepository,
        ICacheService cacheService,
        ILogger<GetAllFlightsQueryHandler> logger)
    {
        _flightRepository = flightRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<GetAllFlightsQueryResponse> Handle(GetAllFlightsQueryRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // CACHE ASIDE PATTERN: Önce cachee bak
            var cachedFlights = await _cacheService.GetAsync<List<FlightDto>>(CacheKey, cancellationToken);
            
            if (cachedFlights != null && cachedFlights.Any())
            {
                _logger.LogInformation("Flights retrieved from cache. Count: {Count}", cachedFlights.Count);
                return new GetAllFlightsQueryResponse
                {
                    Flights = cachedFlights,
                    IsFromCache = true,
                    TotalCount = cachedFlights.Count
                };
            }

            // CACHE STAMPEDE ÖNLEME: Distributed Lock al
            var lockKey = $"{CacheKey}:lock";
            var distributedLock = await _cacheService.AcquireLockAsync(lockKey, LockExpiration, cancellationToken);

            if (distributedLock == null || !distributedLock.IsAcquired)
            {
                // Lock alınamadı, başka bir thread cache'i dolduruyor
                // Kısa bir süre bekle ve tekrar cache'e bak (Double-Check)
                _logger.LogInformation("Lock not acquired, waiting for cache to be populated...");
                await Task.Delay(100, cancellationToken); // 100ms bekle
                
                cachedFlights = await _cacheService.GetAsync<List<FlightDto>>(CacheKey, cancellationToken);
                if (cachedFlights != null && cachedFlights.Any())
                {
                    _logger.LogInformation("Flights retrieved from cache after lock wait. Count: {Count}", cachedFlights.Count);
                    return new GetAllFlightsQueryResponse
                    {
                        Flights = cachedFlights,
                        IsFromCache = true,
                        TotalCount = cachedFlights.Count
                    };
                }
            }

            try
            {
                // Lock alındı veya alınamadı ama cache hala boş - DB'den al
                _logger.LogInformation("Cache miss. Fetching flights from database...");
                
                // DB'den uçuşları al (Soft delete kontrolü + Seats navigation dahil)
                var flights = await _flightRepository
                    .GetQueryable()
                    .Include(f => f.Seats)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var activeFlights = flights.Where(f => !f.IsDeleted).ToList();

                // Domain entity'den DTO'ya manuel mapping
                
                var flightDtos = activeFlights.Select(f => new FlightDto
                {
                    Id = f.Id,
                    FlightNumber = f.FlightNumber,
                    Departure = f.Departure,
                    Destination = f.Destination,
                    DepartureTime = f.DepartureTime,
                    ArrivalTime = f.ArrivalTime,
                    BasePrice = f.BasePrice,
                    Status = f.Status.ToString(),
                    AvailableSeats = f.Seats?.Count(s => !s.IsReserved && !s.IsDeleted) ?? 0,
                    TotalSeats = f.Seats?.Count(s => !s.IsDeleted) ?? 0
                }).ToList();

                // Cache'e kaydet (AWAIT kullan - Fire-and-Forget yerine)
                // Not: Cache yazma işlemi milisaniyeler sürer, await etmek güvenli
                try
                {
                    await _cacheService.SetAsync(CacheKey, flightDtos, CacheExpiration, cancellationToken);
                    _logger.LogInformation("Flights cached successfully. Count: {Count}", flightDtos.Count);
                }
                catch (Exception ex)
                {
                    // Cache yazma hatası kritik değil, sadece logla
                    _logger.LogWarning(ex, "Failed to cache flights. Continuing without cache.");
                }

                _logger.LogInformation("Flights retrieved from database. Count: {Count}", flightDtos.Count);
                
                return new GetAllFlightsQueryResponse
                {
                    Flights = flightDtos,
                    IsFromCache = false,
                    TotalCount = flightDtos.Count
                };
            }
            finally
            {
                // Lock'u serbest bırak
                if (distributedLock != null && distributedLock.IsAcquired)
                {
                    await _cacheService.ReleaseLockAsync(distributedLock, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching flights");
            throw;
        }
    }
}
