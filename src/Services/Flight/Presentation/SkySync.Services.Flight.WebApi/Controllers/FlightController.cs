using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Responses;

namespace SkySync.Services.Flight.WebApi.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
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

            return BadRequest(new { message = response.Message, code = "FLIGHT_CREATE_FAILED" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating flight");
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    }

    /// <summary>
    /// Uçuş oluştururken seçilebilecek uçak listesi (koltuk sayıları farklı demo uçaklar)
    /// </summary>
    [HttpGet("aircrafts")]
    public async Task<IActionResult> GetAircrafts(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _mediator.Send(new GetAircraftsQueryRequest(), cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching aircrafts");
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    }

    /// <summary>
    /// Tüm uçuşları listele (Query - Cache Aside Pattern)
    /// Adım 1: Uçuş arama - Kullanıcı tarih ve rota seçer, özet bilgileri görür
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllFlights([FromQuery] GetAllFlightsQueryRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var query = request ?? new GetAllFlightsQueryRequest();
            var response = await _mediator.Send(query, cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching flights");
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
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
            return NotFound(new { message = ex.Message ?? "Uçuş bulunamadı.", code = "FLIGHT_NOT_FOUND" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching flight seats. FlightId: {FlightId}", flightId);
            return StatusCode(500, new { message = "Bir hata oluştu. Lütfen tekrar deneyin.", code = "INTERNAL_ERROR" });
        }
    }
}
