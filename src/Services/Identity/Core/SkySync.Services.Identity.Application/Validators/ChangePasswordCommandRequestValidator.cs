using FluentValidation;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

namespace SkySync.Services.Identity.Application.Validators;

public class ChangePasswordCommandRequestValidator : AbstractValidator<ChangePasswordCommandRequest>
{
    public ChangePasswordCommandRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mevcut şifre zorunludur.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni şifre zorunludur.")
            .MinimumLength(6).WithMessage("Yeni şifre en az 6 karakter olmalıdır.")
            .Must((request, newPassword) => newPassword != request.CurrentPassword)
            .WithMessage("Yeni şifre mevcut şifre ile aynı olamaz.");
    }
}
