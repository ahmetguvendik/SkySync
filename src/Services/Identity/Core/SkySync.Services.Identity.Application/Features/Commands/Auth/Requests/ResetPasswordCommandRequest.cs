using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class ResetPasswordCommandRequest : IRequest<ResetPasswordCommandResponse>
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
