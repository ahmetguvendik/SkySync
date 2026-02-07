using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Responses;
using SkySync.Services.Identity.Application.Interfaces;
using System.Security.Claims;

namespace SkySync.Services.Identity.Application.Features.Handlers.Auth;

public class LoginCommandHandler : IRequestHandler<LoginCommandRequest, LoginCommandResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginCommandResponse> Handle(LoginCommandRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant(), cancellationToken);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed. Email: {Email}", request.Email);
            return new LoginCommandResponse
            {
                IsSuccess = false,
                Message = "Email veya şifre hatalı."
            };
        }

        var expiryStr = _configuration["JwtSettings:ExpirationMinutes"];
        var expiryMinutes = int.TryParse(expiryStr, out var m) ? m : 60;
        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("firstName", user.FirstName),
            new Claim("lastName", user.LastName)
        };

        var token = _tokenService.GenerateToken(user.Id, user.Email, user.Role, claims);

        _logger.LogInformation("User logged in successfully. UserId: {UserId}, Email: {Email}", user.Id, user.Email);

        return new LoginCommandResponse
        {
            IsSuccess = true,
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserInfoDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role
            }
        };
    }
}
