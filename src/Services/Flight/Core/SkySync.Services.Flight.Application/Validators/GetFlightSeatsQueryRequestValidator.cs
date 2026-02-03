using FluentValidation;
using SkySync.Services.Flight.Application.Features.Queries.Flight.Requests;

namespace SkySync.Services.Flight.Application.Validators;

public class GetFlightSeatsQueryRequestValidator : AbstractValidator<GetFlightSeatsQueryRequest>
{
    public GetFlightSeatsQueryRequestValidator()
    {
        RuleFor(x => x.FlightId)
            .NotEmpty().WithMessage("Uçuş ID zorunludur.");
    }
}
