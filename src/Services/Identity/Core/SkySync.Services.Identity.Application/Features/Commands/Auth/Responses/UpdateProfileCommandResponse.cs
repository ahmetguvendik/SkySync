namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

public class UpdateProfileCommandResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
