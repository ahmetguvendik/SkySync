using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.DTOs;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using AirportEntity = SkySync.Services.Flight.Domain.Entities.Airport;

namespace SkySync.Services.Flight.Application.Features.Handlers.Airport.QueryHandlers;

public class GetAirportsQueryHandler : IRequestHandler<GetAirportsQueryRequest, GetAirportsQueryResponse>
{
    private readonly IGenericRepository<AirportEntity> _airportRepository;
    private readonly ILogger<GetAirportsQueryHandler> _logger;
    private readonly ICacheService _cacheService;

    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;
    private const string AirportsCacheKey = "airports:all";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(6);
    private static readonly TimeSpan LockExpiration = TimeSpan.FromSeconds(5);

    public GetAirportsQueryHandler(
        IGenericRepository<AirportEntity> airportRepository,
        ILogger<GetAirportsQueryHandler> logger,
        ICacheService cacheService)
    {
        _airportRepository = airportRepository;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<GetAirportsQueryResponse> Handle(GetAirportsQueryRequest request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);
        var pageSize = NormalizePageSize(request.PageSize);
        var normalizedSearch = NormalizeSearch(request.Search);
        var (airportsSnapshot, isFromCache) = await GetAirportsSnapshotAsync(cancellationToken);

        var filteredAirports = FilterAirports(airportsSnapshot, normalizedSearch);
        var totalCount = filteredAirports.Count;
        var pagedAirports = ApplyPaging(filteredAirports, page, pageSize);

        return new GetAirportsQueryResponse
        {
            Airports = pagedAirports,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            IsFromCache = isFromCache
        };
    }

    private async Task<(IReadOnlyList<AirportDto> Airports, bool IsFromCache)> GetAirportsSnapshotAsync(CancellationToken cancellationToken)
    {
        var cachedAirports = await _cacheService.GetAsync<List<AirportDto>>(AirportsCacheKey, cancellationToken);
        if (cachedAirports != null)
        {
            _logger.LogDebug("Airport cache hit. Count={Count}", cachedAirports.Count);
            return (cachedAirports, true);
        }

        var lockKey = $"{AirportsCacheKey}:lock";
        var distributedLock = await _cacheService.AcquireLockAsync(lockKey, LockExpiration, cancellationToken);

        if (distributedLock == null || !distributedLock.IsAcquired)
        {
            _logger.LogDebug("Airport cache lock not acquired. Waiting briefly...");
            await Task.Delay(50, cancellationToken);
            cachedAirports = await _cacheService.GetAsync<List<AirportDto>>(AirportsCacheKey, cancellationToken);
            if (cachedAirports != null)
            {
                return (cachedAirports, true);
            }
        }

        try
        {
            var airports = await _airportRepository
                .GetQueryable()
                .AsNoTracking()
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.City)
                .ThenBy(a => a.Name)
                .ToListAsync(cancellationToken);

            var dtos = airports.Select(a => new AirportDto
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                City = a.City,
                Country = a.Country
            }).ToList();

            await _cacheService.SetAsync(AirportsCacheKey, dtos, CacheExpiration, cancellationToken);
            _logger.LogInformation("Airport cache refreshed from database. Count={Count}", dtos.Count);

            return (dtos, false);
        }
        finally
        {
            if (distributedLock != null && distributedLock.IsAcquired)
                await _cacheService.ReleaseLockAsync(distributedLock, cancellationToken);
        }
    }

    private static int NormalizePage(int page) => page <= 0 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
    {
        if (pageSize <= 0)
            return DefaultPageSize;

        return Math.Min(pageSize, MaxPageSize);
    }

    private static string? NormalizeSearch(string? search)
    {
        return string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();
    }

    private static List<AirportDto> FilterAirports(IEnumerable<AirportDto> airports, string? normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
            return airports.ToList();

        return airports.Where(a =>
                a.Code.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                a.City.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                a.Country.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<AirportDto> ApplyPaging(List<AirportDto> airports, int page, int pageSize)
    {
        var skip = (page - 1) * pageSize;
        if (skip < 0)
            skip = 0;

        return airports
            .Skip(skip)
            .Take(pageSize)
            .ToList();
    }
}
