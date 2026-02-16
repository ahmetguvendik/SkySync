using MediatR;
using Microsoft.Extensions.Logging;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Responses;
using SkySync.Services.Flight.Application.Interfaces;
using SkySync.Services.Flight.Application.UnitOfWorks;
using AirportEntity = SkySync.Services.Flight.Domain.Entities.Airport;

namespace SkySync.Services.Flight.Application.Features.Handlers.Airport;

public class CreateAirportCommandHandler : IRequestHandler<CreateAirportCommandRequest, CreateAirportCommandResponse>
{
    private readonly IGenericRepository<AirportEntity> _airportRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAirportCommandHandler> _logger;
    private readonly ICacheService _cacheService;
    private const string AirportsCacheKey = "airports:all";

    public CreateAirportCommandHandler(
        IGenericRepository<AirportEntity> airportRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateAirportCommandHandler> logger,
        ICacheService cacheService)
    {
        _airportRepository = airportRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
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

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var airport = new AirportEntity
            {
                Id = Guid.NewGuid(),
                Code = request.Code.ToUpperInvariant(),
                Name = request.Name,
                City = request.City,
                Country = request.Country
            };

            await _airportRepository.CreateAsync(airport, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            await _cacheService.RemoveAsync(AirportsCacheKey, cancellationToken);

            _logger.LogInformation("Airport created: {Code} - {Name}", airport.Code, airport.Name);

            return new CreateAirportCommandResponse
            {
                AirportId = airport.Id,
                IsSuccess = true,
                Message = "Airport created successfully."
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Error occurred while creating airport. Code: {Code}, Error: {Error}",
                request.Code, ex.Message);

            return new CreateAirportCommandResponse
            {
                AirportId = Guid.Empty,
                IsSuccess = false,
                Message = $"Error occurred while creating airport: {ex.Message}"
            };
        }
    }
}
