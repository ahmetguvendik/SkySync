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

    public GetAirportsQueryHandler(
        IGenericRepository<AirportEntity> airportRepository,
        ILogger<GetAirportsQueryHandler> logger)
    {
        _airportRepository = airportRepository;
        _logger = logger;
    }

    public async Task<GetAirportsQueryResponse> Handle(GetAirportsQueryRequest request, CancellationToken cancellationToken)
    {
        var query = _airportRepository
            .GetQueryable()
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToUpperInvariant();
            query = query.Where(a => a.Code.ToUpper().Contains(search));
        }

        var airports = await query
            .OrderBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var dtos = airports.Select(a => new AirportDto
        {
            Id = a.Id,
            Code = a.Code,
            Name = a.Name,
            City = a.City,
            Country = a.Country
        }).ToList();

        _logger.LogInformation("Airports fetched. Count: {Count}", dtos.Count);

        return new GetAirportsQueryResponse { Airports = dtos };
    }
}
