using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Payment.Application.Features.Commands.Payment.Requests;

namespace SkySync.Services.Payment.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Ödemeyi tetikler (frontend kart ekranından "Öde" butonu).
    /// Rezervasyon response'dan CorrelationId, ReservationId, Amount, ExpiresAt alınır.
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentCommandRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(request, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }
}
