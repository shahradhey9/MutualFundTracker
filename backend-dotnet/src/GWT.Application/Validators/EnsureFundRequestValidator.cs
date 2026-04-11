using FluentValidation;
using GWT.Application.DTOs.Funds;

namespace GWT.Application.Validators;

public class EnsureFundRequestValidator : AbstractValidator<EnsureFundRequestDto>
{
    public EnsureFundRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Fund Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Fund name is required.")
            .MaximumLength(500);

        RuleFor(x => x.Amc)
            .NotEmpty().WithMessage("AMC/exchange name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Ticker)
            .NotEmpty().WithMessage("Ticker is required.")
            .MaximumLength(50);
    }
}
