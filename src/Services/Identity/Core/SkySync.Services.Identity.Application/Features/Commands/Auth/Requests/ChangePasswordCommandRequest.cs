using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using System.Text.Json.Serialization;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class ChangePasswordCommandRequest : IRequest<ChangePasswordCommandResponse>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
