using MediatR;
using SkySync.Services.Identity.Application.Features.Queries.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Queries.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class GetProfileQueryHandler : IRequestHandler<GetProfileQueryRequest, GetProfileQueryResponse?>
{
    private readonly IUserRepository _userRepository;

    public GetProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetProfileQueryResponse?> Handle(GetProfileQueryRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
            return null;

        return new GetProfileQueryResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role
        };
    }
}
