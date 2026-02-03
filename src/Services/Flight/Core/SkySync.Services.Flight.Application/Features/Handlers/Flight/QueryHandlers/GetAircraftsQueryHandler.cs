using MediatR;
using SkySync.Services.Flight.Application.DTOs;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;
using SkySync.Services.Flight.Application.Interfaces;

namespace SkySync.Services.Flight.Application.Features.Handlers.Flight.QueryHandlers;

public class GetAircraftsQueryHandler : IRequestHandler<GetAircraftsQueryRequest, GetAircraftsQueryResponse>
{
    private readonly IAircraftRepository _aircraftRepository;

    public GetAircraftsQueryHandler(IAircraftRepository aircraftRepository)
    {
        _aircraftRepository = aircraftRepository;
    }

    public async Task<GetAircraftsQueryResponse> Handle(GetAircraftsQueryRequest request, CancellationToken cancellationToken)
    {
        var list = await _aircraftRepository.GetAllAsync(cancellationToken);
        return new GetAircraftsQueryResponse
        {
            Aircraft = list.Select(a => new AircraftDto
            {
                Id = a.Id,
                Name = a.Name,
                SeatCount = a.SeatCount
            }).ToList()
        };
    }
}
