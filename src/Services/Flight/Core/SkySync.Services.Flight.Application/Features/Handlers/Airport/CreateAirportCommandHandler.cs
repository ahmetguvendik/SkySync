using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using AirportEntity = SkySync.Services.Flight.Domain.Entities.Airport;

namespace SkySync.Services.Flight.Application.Features.Handlers.Airport;

public class CreateAirportCommandHandler : IRequestHandler<CreateAirportCommandRequest, CreateAirportCommandResponse>
{
    private readonly IGenericRepository<AirportEntity> _airportRepository;
    private readonly ILogger<CreateAirportCommandHandler> _logger;

    public CreateAirportCommandHandler(
        IGenericRepository<AirportEntity> airportRepository,
        ILogger<CreateAirportCommandHandler> logger)
    {
        _airportRepository = airportRepository;
        _logger = logger;
    }

    public async Task<CreateAirportCommandResponse> Handle(CreateAirportCommandRequest request, CancellationToken cancellationToken)
    {
        var existing = await _airportRepository
            .GetAllAsync(a => a.Code == request.Code, cancellationToken);

        if (existing.Any())
        {
            return new CreateAirportCommandResponse
            {
                AirportId = existing.First().Id,
                IsSuccess = false,
                Message = $"Airport with code {request.Code} already exists."
            };
        }

        var airport = new AirportEntity
        {
            Id = Guid.NewGuid(),
            Code = request.Code.ToUpperInvariant(),
            Name = request.Name,
            City = request.City,
            Country = request.Country
        };

        await _airportRepository.CreateAsync(airport, cancellationToken);

        _logger.LogInformation("Airport created: {Code} - {Name}", airport.Code, airport.Name);

        return new CreateAirportCommandResponse
        {
            AirportId = airport.Id,
            IsSuccess = true,
            Message = "Airport created successfully."
        };
    }
}
