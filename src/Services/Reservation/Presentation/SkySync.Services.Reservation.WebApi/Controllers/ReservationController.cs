using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Requests;

namespace SkySync.Services.Reservation.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[Authorize]
public class ReservationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReservationController> _logger;

    public ReservationController(IMediator mediator, ILogger<ReservationController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Yeni rezervasyon oluştur (Command)
    /// Saga State Machine'i tetikler
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationCommandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var response = await _mediator.Send(request, cancellationToken);

            if (response.IsSuccess)
            {
                return CreatedAtAction(nameof(CreateReservation), new { id = response.ReservationId }, response);
            }

            return BadRequest(new { message = response.Message, code = "RESERVATION_CREATE_FAILED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating reservation");
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    }

    /// <summary>
    /// Yolcu rezervasyonlarını listele (Query)
    /// </summary>
    [HttpGet("passenger/{passengerEmail}")]
    public async Task<IActionResult> GetPassengerReservations(string passengerEmail, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetPassengerReservationsQueryRequest
            {
                PassengerEmail = passengerEmail,
                Page = page > 0 ? page : 1
            };
            var response = await _mediator.Send(query, cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching passenger reservations. Email: {Email}", passengerEmail);
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    }
}
