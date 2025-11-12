using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class BootstrapCompleteRequestValidator : AbstractValidator<BootstrapCompleteRequest>
{
    public BootstrapCompleteRequestValidator()
    {
        RuleFor(x => x.SetupToken)
            .NotEmpty()
            .When(x => string.IsNullOrWhiteSpace(x.Email) || string.IsNullOrWhiteSpace(x.Password));
    }
}
