using FluentValidation;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

namespace SkySync.Services.Identity.Application.Validators;

public class ResetPasswordCommandRequestValidator : AbstractValidator<ResetPasswordCommandRequest>
{
    public ResetPasswordCommandRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Token zorunludur.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre zorunludur.")
            .MinimumLength(6).WithMessage("Şifre en az 6 karakter olmalıdır.");
    }
}
