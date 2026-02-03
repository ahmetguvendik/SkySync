using FluentValidation;
using SkySync.Services.Flight.Application.Features.Commands.Flight.Requests;

namespace SkySync.Services.Flight.Application.Validators;

public class CreateFlightCommandRequestValidator : AbstractValidator<CreateFlightCommandRequest>
{
    public CreateFlightCommandRequestValidator()
    {
        RuleFor(x => x.AircraftId)
            .NotEmpty().WithMessage("Uçak seçimi zorunludur.");

        RuleFor(x => x.FlightNumber)
            .NotEmpty().WithMessage("Uçuş numarası zorunludur.")
            .MaximumLength(20).WithMessage("Uçuş numarası en fazla 20 karakter olabilir.");

        RuleFor(x => x.Departure)
            .NotEmpty().WithMessage("Kalkış noktası zorunludur.")
            .MaximumLength(100).WithMessage("Kalkış noktası en fazla 100 karakter olabilir.");

        RuleFor(x => x.Destination)
            .NotEmpty().WithMessage("Varış noktası zorunludur.")
            .MaximumLength(100).WithMessage("Varış noktası en fazla 100 karakter olabilir.");

        RuleFor(x => x.DepartureTime)
            .NotEmpty().WithMessage("Kalkış saati zorunludur.");

        RuleFor(x => x.ArrivalTime)
            .NotEmpty().WithMessage("Varış saati zorunludur.")
            .GreaterThan(x => x.DepartureTime).WithMessage("Varış saati kalkış saatinden sonra olmalıdır.");

        RuleFor(x => x.BasePrice)
            .GreaterThanOrEqualTo(0).WithMessage("Taban fiyat 0 veya pozitif olmalıdır.");
    }
}
