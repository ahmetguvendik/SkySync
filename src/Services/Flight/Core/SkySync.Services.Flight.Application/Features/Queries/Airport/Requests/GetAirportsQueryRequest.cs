using MediatR;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Responses;

namespace SkySync.Services.Flight.Application.Features.Queries.Airport.Requests;

public class GetAirportsQueryRequest : IRequest<GetAirportsQueryResponse>
{
    public string? Search { get; set; }
}
