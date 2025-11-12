using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class BootstrapCompleteRequestValidator : AbstractValidator<BootstrapCompleteRequest>
{
    public BootstrapCompleteRequestValidator()
    {
        RuleFor(x => x)
            .Must(HasValidPayload)
            .WithMessage("Either setupToken or email/password must be provided");

        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email!)
                .EmailAddress()
                .MaximumLength(255);
        });

        When(x => !string.IsNullOrWhiteSpace(x.Password), () =>
        {
            RuleFor(x => x.Password!)
                .MinimumLength(12);
        });
    }

    private static bool HasValidPayload(BootstrapCompleteRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SetupToken))
            return true;

        return !string.IsNullOrWhiteSpace(request.Email) && !string.IsNullOrWhiteSpace(request.Password);
    }
}
