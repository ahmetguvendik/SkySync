using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Queries.Auth.Requests;
using SkySync.Services.Identity.Application.Features.Queries.Users.Requests;
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
    /// Sistemdeki kullanıcıları listele (Admin)
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUsers([FromQuery] GetUsersQueryRequest request, CancellationToken cancellationToken)
    {
        var query = request ?? new GetUsersQueryRequest();
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Profil bilgilerini güncelle (Ad, Soyad, E-posta)
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommandRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Yetkisiz erişim.", code = "UNAUTHORIZED" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        request.UserId = userId;
        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message, code = "PROFILE_UPDATE_FAILED" });

        return Ok(new { message = response.Message });
    }

    /// <summary>
    /// Şifreyi mevcut şifreyi doğrulayarak değiştir
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommandRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Yetkisiz erişim.", code = "UNAUTHORIZED" });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        request.UserId = userId;
        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message, code = "PASSWORD_CHANGE_FAILED" });

        return Ok(new { message = response.Message });
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
    /// Admin kullanıcı kaydı (sadece Admin yetkili)
    /// </summary>
    [HttpPost("register/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterAdmin([FromBody] CreateAdminCommandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message, code = "ADMIN_REGISTER_FAILED" });

        return CreatedAtAction(nameof(RegisterAdmin), new { userId = response.UserId }, response);
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
    /// Şifre sıfırlama bağlantısı gönderir (kullanıcı kayıtlıysa)
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _mediator.Send(request, cancellationToken);
        return Ok(new { message = response.Message });
    }

    /// <summary>
    /// Şifreyi verilen token ile günceller
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message, code = "RESET_PASSWORD_FAILED" });

        return Ok(new { message = response.Message });
    }

    /// <summary>
    /// Email doğrulama tokenını onaylar
    /// </summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCommandRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(request, cancellationToken);
        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message, code = "EMAIL_VERIFY_FAILED" });

        return Ok(new { message = response.Message });
    }

    /// <summary>
    /// Kullanıcı profili - JWT gerekli
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Yetkisiz erişim.", code = "UNAUTHORIZED" });

        var query = new GetProfileQueryRequest { UserId = userId };
        var response = await _mediator.Send(query, cancellationToken);

        if (response == null)
            return NotFound(new { message = "Kullanıcı bulunamadı.", code = "USER_NOT_FOUND" });

        return Ok(response);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId);
    }
}
