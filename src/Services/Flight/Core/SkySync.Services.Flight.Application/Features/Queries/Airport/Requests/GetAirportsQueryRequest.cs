using MediatR;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Responses;

namespace SkySync.Services.Flight.Application.Features.Queries.Airport.Requests;

public class GetAirportsQueryRequest : IRequest<GetAirportsQueryResponse>
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
