using FluentValidation;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;

namespace SkySync.Services.Reservation.Application.Validators;

public class UpdateReservationStatusCommandRequestValidator : AbstractValidator<UpdateReservationStatusCommandRequest>
{
    public UpdateReservationStatusCommandRequestValidator()
    {
        RuleFor(x => x.ReservationId)
            .NotEmpty().WithMessage("Rezervasyon ID zorunludur.");
    }
}
