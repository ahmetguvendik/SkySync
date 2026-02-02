using MediatR;
using SkySync.Services.Identity.Application.Features.Queries.Auth.Responses;

namespace SkySync.Services.Identity.Application.Features.Queries.Auth.Requests;

public class GetProfileQueryRequest : IRequest<GetProfileQueryResponse?>
{
    public Guid UserId { get; set; }
}
