using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Requests;
using SkySync.Services.Flight.Application.Features.Queries.Airport.Requests;

namespace SkySync.Services.Flight.WebApi.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class AirportController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AirportController> _logger;

    public AirportController(IMediator mediator, ILogger<AirportController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAirports([FromQuery] GetAirportsQueryRequest? request, CancellationToken cancellationToken)
    {
        var query = request ?? new GetAirportsQueryRequest();
        var response = await _mediator.Send(query, cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAirport([FromBody] CreateAirportCommandRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _mediator.Send(request, cancellationToken);
        if (!response.IsSuccess)
            return BadRequest(new { message = response.Message });

        _logger.LogInformation("Airport created via API: {Code}", request.Code);
        return CreatedAtAction(nameof(GetAirports), new { code = request.Code }, response);
    }
}
