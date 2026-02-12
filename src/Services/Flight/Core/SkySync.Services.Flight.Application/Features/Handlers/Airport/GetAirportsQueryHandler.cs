using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.DTOs;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using AirportEntity = SkySync.Services.Flight.Domain.Entities.Airport;

namespace SkySync.Services.Flight.Application.Features.Handlers.Airport;

public class GetAirportsQueryHandler : IRequestHandler<GetAirportsQueryRequest, GetAirportsQueryResponse>
{
    private readonly IGenericRepository<AirportEntity> _airportRepository;
    private readonly ILogger<GetAirportsQueryHandler> _logger;

    private const int PageSize = 10; // Her sayfada 10 havalimanı

    public GetAirportsQueryHandler(
        IGenericRepository<AirportEntity> airportRepository,
        ILogger<GetAirportsQueryHandler> logger)
    {
        _airportRepository = airportRepository;
        _logger = logger;
    }

    public async Task<GetAirportsQueryResponse> Handle(GetAirportsQueryRequest request, CancellationToken cancellationToken)
    {
        var page = NormalizePage(request.Page);

        var query = _airportRepository
            .GetQueryable()
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToUpperInvariant();
            query = query.Where(a =>
                a.Code.ToUpper().Contains(search) ||
                a.Name.ToUpper().Contains(search) ||
                a.City.ToUpper().Contains(search) ||
                a.Country.ToUpper().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var airports = await query
            .OrderBy(a => a.City)
            .ThenBy(a => a.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var dtos = airports.Select(a => new AirportDto
        {
            Id = a.Id,
            Code = a.Code,
            Name = a.Name,
            City = a.City,
            Country = a.Country
        }).ToList();

        _logger.LogInformation("Airports fetched. Count: {Count}, Total: {TotalCount}, Page: {Page}",
            dtos.Count, totalCount, page);

        return new GetAirportsQueryResponse
        {
            Airports = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = PageSize
        };
    }

    private static int NormalizePage(int page) => page <= 0 ? 1 : page;
}
