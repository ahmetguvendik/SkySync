using FluentValidation;
using SkySync.Services.Reservation.Application.Features.Commands.Reservation.Requests;

namespace SkySync.Services.Reservation.Application.Validators;

public class CreateReservationCommandRequestValidator : AbstractValidator<CreateReservationCommandRequest>
{
    public CreateReservationCommandRequestValidator()
    {
        RuleFor(x => x.FlightId)
            .NotEmpty().WithMessage("Uçuş ID zorunludur.");

        RuleFor(x => x.SeatNumber)
            .NotEmpty().WithMessage("Koltuk numarası zorunludur.")
            .MaximumLength(10).WithMessage("Koltuk numarası en fazla 10 karakter olabilir.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Fiyat 0'dan büyük olmalıdır.");

        RuleFor(x => x.PassengerName)
            .NotEmpty().WithMessage("Yolcu adı zorunludur.")
            .MaximumLength(100).WithMessage("Yolcu adı en fazla 100 karakter olabilir.");

        RuleFor(x => x.PassengerSurname)
            .NotEmpty().WithMessage("Yolcu soyadı zorunludur.")
            .MaximumLength(100).WithMessage("Yolcu soyadı en fazla 100 karakter olabilir.");

        RuleFor(x => x.PassengerEmail)
            .NotEmpty().WithMessage("Yolcu e-posta adresi zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.")
            .MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir.");
    }
}
