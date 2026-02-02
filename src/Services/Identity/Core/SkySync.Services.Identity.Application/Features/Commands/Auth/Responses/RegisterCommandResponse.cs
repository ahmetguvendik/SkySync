namespace SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;

public class RegisterCommandResponse
{
    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public Guid? UserId { get; set; }
}
