using FluentValidation;
using SkySync.Services.Flight.Application.Features.Commands.Airport.Requests;

namespace SkySync.Services.Flight.Application.Validators;

public class CreateAirportCommandRequestValidator : AbstractValidator<CreateAirportCommandRequest>
{
    public CreateAirportCommandRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(10);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(x => x.City)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Country)
            .NotEmpty()
            .MaximumLength(100);
    }
}
