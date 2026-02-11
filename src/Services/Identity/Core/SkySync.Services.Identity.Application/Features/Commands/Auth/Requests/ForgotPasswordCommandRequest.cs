using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class ForgotPasswordCommandRequest : IRequest<ForgotPasswordCommandResponse>
{
    public string Email { get; set; } = string.Empty;
}
