using System;
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

    private const string CacheKeyPrefix = "flights:search";
    private static readonly TimeSpan CacheAllExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CacheFilteredExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(10); // Lock için maksimum süre
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

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
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        try
        {
            var hasFilters = HasSearchFilters(request);
            var cacheKey = BuildCacheKey(request, page, pageSize);
            var cacheExpiration = hasFilters ? CacheFilteredExpiration : CacheAllExpiration;
            var cachedResponse = await _cacheService.GetAsync<GetAllFlightsQueryResponse>(cacheKey, cancellationToken);

            if (cachedResponse != null)
            {
                cachedResponse.IsFromCache = true;
                _logger.LogInformation(
                    "Flights served from cache. FiltersApplied={HasFilters}, Page={Page}, PageSize={PageSize}",
                    hasFilters,
                    page,
                    pageSize);
                return cachedResponse;
            }

            var lockKey = $"{cacheKey}:lock";
            var distributedLock = await _cacheService.AcquireLockAsync(lockKey, LockExpiration, cancellationToken);

            if (distributedLock == null || !distributedLock.IsAcquired)
            {
                _logger.LogInformation("Cache lock not acquired for {CacheKey}. Waiting briefly...", cacheKey);
                await Task.Delay(100, cancellationToken);

                cachedResponse = await _cacheService.GetAsync<GetAllFlightsQueryResponse>(cacheKey, cancellationToken);
                if (cachedResponse != null)
                {
                    cachedResponse.IsFromCache = true;
                    return cachedResponse;
                }
            }

            try
            {
                var response = await FetchFlightsAsync(request, page, pageSize, cancellationToken);
                await _cacheService.SetAsync(cacheKey, response, cacheExpiration, cancellationToken);
                return response;
            }
            finally
            {
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

    private async Task<GetAllFlightsQueryResponse> FetchFlightsAsync(
        GetAllFlightsQueryRequest request,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _flightRepository
            .GetQueryable()
            .AsNoTracking()
            .Where(f => !f.IsDeleted);

        query = ApplyFilters(query, request);

        var totalCount = await query.CountAsync(cancellationToken);

        var pagedFlights = await query
            .OrderBy(f => f.DepartureTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(f => f.Seats)
            .ToListAsync(cancellationToken);

        var flightDtos = pagedFlights.Select(f => new FlightDto
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

        _logger.LogInformation(
            "Flights retrieved from database. Departure={Departure}, Destination={Destination}, Date={DepartureDate}, Page={Page}, Size={PageSize}, Returned={Count}, Total={TotalCount}",
            request.Departure,
            request.Destination,
            request.DepartureDate?.ToString("yyyy-MM-dd"),
            page,
            pageSize,
            flightDtos.Count,
            totalCount);

        return new GetAllFlightsQueryResponse
        {
            Flights = flightDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            IsFromCache = false
        };
    }

    private static IQueryable<FlightEntity> ApplyFilters(IQueryable<FlightEntity> query, GetAllFlightsQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Departure))
        {
            query = query.Where(f => f.Departure == request.Departure);
        }

        if (!string.IsNullOrWhiteSpace(request.Destination))
        {
            query = query.Where(f => f.Destination == request.Destination);
        }

        if (request.DepartureDate.HasValue)
        {
            // DateOnly -> DateTime dönüşümünde Kind = Unspecified geliyor.
            // Npgsql, timestamptz için yalnızca UTC DateTime kabul ediyor.
            var startUnspecified = request.DepartureDate.Value.ToDateTime(TimeOnly.MinValue);
            var start = DateTime.SpecifyKind(startUnspecified, DateTimeKind.Utc);
            var end = start.AddDays(1); // Kind = Utc olarak devam eder

            query = query.Where(f => f.DepartureTime >= start && f.DepartureTime < end);
        }

        return query;
    }

    private static int NormalizePage(int page) => page <= 0 ? 1 : page;

    private static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static bool HasSearchFilters(GetAllFlightsQueryRequest request) =>
        !string.IsNullOrWhiteSpace(request.Departure) ||
        !string.IsNullOrWhiteSpace(request.Destination) ||
        request.DepartureDate.HasValue ||
        request.ReturnDate.HasValue; // ReturnDate ileride kullanılacak olsa da cache dışı tutulur

    private static string BuildCacheKey(GetAllFlightsQueryRequest request, int page, int pageSize)
    {
        var departure = NormalizeKeyPart(request.Departure);
        var destination = NormalizeKeyPart(request.Destination);
        var departureDate = request.DepartureDate?.ToString("yyyyMMdd") ?? "any";
        var returnDate = request.ReturnDate?.ToString("yyyyMMdd") ?? "any";

        return $"{CacheKeyPrefix}:dep={departure}:dest={destination}:depDate={departureDate}:retDate={returnDate}:p={page}:s={pageSize}";
    }

    private static string NormalizeKeyPart(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "any" : value.Trim().ToUpperInvariant();
}
