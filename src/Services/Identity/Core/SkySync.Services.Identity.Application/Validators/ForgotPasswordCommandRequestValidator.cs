using FluentValidation;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

namespace SkySync.Services.Identity.Application.Validators;

public class ForgotPasswordCommandRequestValidator : AbstractValidator<ForgotPasswordCommandRequest>
{
    public ForgotPasswordCommandRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta giriniz.")
            .MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir.");
    }
}
