using FluentValidation;
using SkySync.Services.Reservation.Application.Features.Queries.Reservation.Requests;

namespace SkySync.Services.Reservation.Application.Validators;

public class GetPassengerReservationsQueryRequestValidator : AbstractValidator<GetPassengerReservationsQueryRequest>
{
    public GetPassengerReservationsQueryRequestValidator()
    {
        RuleFor(x => x.PassengerEmail)
            .NotEmpty().WithMessage("Yolcu e-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Page)
            .GreaterThan(0).WithMessage("Sayfa numarası 1 veya daha büyük olmalıdır.");
    }
}
