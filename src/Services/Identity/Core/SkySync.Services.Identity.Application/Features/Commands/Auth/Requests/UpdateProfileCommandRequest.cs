using MediatR;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using System.Text.Json.Serialization;

namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

public class UpdateProfileCommandRequest : IRequest<UpdateProfileCommandResponse>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
