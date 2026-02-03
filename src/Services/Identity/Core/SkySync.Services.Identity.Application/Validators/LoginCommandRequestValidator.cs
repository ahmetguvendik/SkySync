using FluentValidation;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

namespace SkySync.Services.Identity.Application.Validators;

public class LoginCommandRequestValidator : AbstractValidator<LoginCommandRequest>
{
    public LoginCommandRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Şifre zorunludur.");
    }
}
