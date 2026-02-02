using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class LoginCommandRequest : IRequest<LoginCommandResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
