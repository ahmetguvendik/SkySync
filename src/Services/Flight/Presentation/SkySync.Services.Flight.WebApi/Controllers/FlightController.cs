using MediatR;
using Microsoft.AspNetCore.Mvc;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Responses;

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

    [HttpPost]
    [ProducesResponseType(typeof(CreateFlightCommandResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
}