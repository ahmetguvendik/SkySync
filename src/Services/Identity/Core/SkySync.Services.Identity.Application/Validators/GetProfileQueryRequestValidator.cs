using FluentValidation;
using SkySync.Services.Identity.Application.Features.Queries.Auth.Requests;

namespace SkySync.Services.Identity.Application.Validators;

public class GetProfileQueryRequestValidator : AbstractValidator<GetProfileQueryRequest>
{
    public GetProfileQueryRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("Kullanıcı ID zorunludur.");
    }
}
