using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Queries.Auth.Requests;
using System.Security.Claims;

namespace SkySync.Services.Identity.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Yeni kullanıcı kaydı
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterCommandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message, code = "REGISTER_FAILED" });

        return CreatedAtAction(nameof(Register), new { userId = response.UserId }, response);
    }

    /// <summary>
    /// Giriş - JWT token döner
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommandRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            return BadRequest(new { message = "Email ve şifre gerekli.", code = "VALIDATION_ERROR" });

        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
            return Unauthorized(new { message = response.Message, code = "LOGIN_FAILED" });

        return Ok(new
        {
            token = response.Token,
            expiresAt = response.ExpiresAt,
            user = response.User
        });
    }

    /// <summary>
    /// Kullanıcı profili - JWT gerekli
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Yetkisiz erişim.", code = "UNAUTHORIZED" });

        var query = new GetProfileQueryRequest { UserId = userId };
        var response = await _mediator.Send(query, cancellationToken);

        if (response == null)
            return NotFound(new { message = "Kullanıcı bulunamadı.", code = "USER_NOT_FOUND" });

        return Ok(response);
    }
}
