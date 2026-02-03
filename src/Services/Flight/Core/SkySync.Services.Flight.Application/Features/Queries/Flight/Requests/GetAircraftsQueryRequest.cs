using MediatR;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

namespace SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;

public class GetAircraftsQueryRequest : IRequest<GetAircraftsQueryResponse>
{
}
