using FluentValidation;
using GWT.Application.DTOs.Portfolio;

namespace GWT.Application.Validators;

public class AddHoldingRequestValidator : AbstractValidator<AddHoldingRequestDto>
{
    public AddHoldingRequestValidator()
    {
        RuleFor(x => x.FundId)
            .NotEmpty().WithMessage("FundId is required.");

        RuleFor(x => x.Units)
            .GreaterThan(0).WithMessage("Units must be greater than zero.");

        RuleFor(x => x.AvgCost)
            .GreaterThan(0).WithMessage("Average cost must be greater than zero.")
            .When(x => x.AvgCost.HasValue);

        RuleFor(x => x.PurchaseAt)
            .NotEmpty().WithMessage("Purchase date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("Purchase date cannot be in the future.");
    }
}
