using FluentValidation;
using GWT.Application.DTOs.Auth;

namespace GWT.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
