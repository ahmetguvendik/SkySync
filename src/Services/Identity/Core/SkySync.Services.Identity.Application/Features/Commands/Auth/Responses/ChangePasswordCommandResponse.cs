namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

public class ChangePasswordCommandResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}
