using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class VerifyEmailCommandRequest : IRequest<VerifyEmailCommandResponse>
{
    public string Token { get; set; } = string.Empty;
}
