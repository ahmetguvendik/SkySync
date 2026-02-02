using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class RegisterCommandRequest : IRequest<RegisterCommandResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
