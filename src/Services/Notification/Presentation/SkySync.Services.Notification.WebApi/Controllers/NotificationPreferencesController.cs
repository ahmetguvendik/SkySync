using System;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Subscribe;
using SkySync.Services.Notification.Application.Features.NotificationPreferences.Commands.Unsubscribe;

namespace SkySync.Services.Notification.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/notification/preferences")]
[ApiVersion("1.0")]
public class NotificationPreferencesController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationPreferencesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("unsubscribe/{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Unsubscribe(Guid token, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new UnsubscribeNotificationCommandRequest { Token = token },
            cancellationToken);

        if (!response.IsSuccess)
            return NotFound(new { message = response.Message });

        return Ok(new
        {
            message = response.Message,
            email = response.Email,
            receivesOperationalEmails = false
        });
    }

    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> Resubscribe(
        [FromBody] SubscribeNotificationCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.UserId == Guid.Empty)
            return BadRequest(new { message = "Geçersiz kullanıcı bilgisi." });

        var response = await _mediator.Send(request, cancellationToken);

        if (!response.IsSuccess)
        {
            if (response.IsNotFound)
                return NotFound(new { message = response.Message });
            return BadRequest(new { message = response.Message });
        }

        return Ok(new
        {
            message = response.Message,
            receivesOperationalEmails = true
        });
    }
}
