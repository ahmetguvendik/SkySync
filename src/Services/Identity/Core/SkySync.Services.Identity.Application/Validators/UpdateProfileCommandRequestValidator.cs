using FluentValidation;
using SkySync.Services.Identity.Application.Features.Commands.Auth.Requests;

namespace SkySync.Services.Identity.Application.Validators;

public class UpdateProfileCommandRequestValidator : AbstractValidator<UpdateProfileCommandRequest>
{
    public UpdateProfileCommandRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Ad zorunludur.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Soyad zorunludur.")
            .MaximumLength(100).WithMessage("Soyad en fazla 100 karakter olabilir.");
    }
}
