using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;

namespace SkySync.Services.Flight.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlightController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<FlightController> _logger;

    public FlightController(IMediator mediator, ILogger<FlightController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Yeni uçuş oluştur (Command)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateFlight([FromBody] CreateFlightCommandRequest request, CancellationToken cancellationToken)
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
                return CreatedAtAction(nameof(CreateFlight), new { id = response.FlightId }, response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating flight");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Tüm uçuşları listele (Query - Cache Aside Pattern)
    /// Adım 1: Uçuş arama - Kullanıcı tarih ve rota seçer, özet bilgileri görür
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllFlights(CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetAllFlightsQueryRequest();
            var response = await _mediator.Send(query, cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching flights");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Belirli bir uçuşun koltuklarını getir (Query - Direct DB)
    /// Adım 2: Koltuk seçimi - Kullanıcı uçuşu beğendi, koltuk haritasını görür
    /// </summary>
    [HttpGet("{flightId}/seats")]
    public async Task<IActionResult> GetFlightSeats(Guid flightId, CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetFlightSeatsQueryRequest { FlightId = flightId };
            var response = await _mediator.Send(query, cancellationToken);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Flight not found. FlightId: {FlightId}", flightId);
            return NotFound(new { message = $"Flight with id {flightId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching flight seats. FlightId: {FlightId}", flightId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}