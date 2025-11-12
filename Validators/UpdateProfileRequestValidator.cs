using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress()
                .MaximumLength(255);
        });

        When(x => !string.IsNullOrEmpty(x.NewPassword), () =>
        {
            RuleFor(x => x.CurrentPassword)
                .NotEmpty()
                .WithMessage("Current password is required to change password");

            RuleFor(x => x.NewPassword)
                .MinimumLength(8)
                .MaximumLength(100)
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
                .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one digit")
                .Matches(@"[\W_]").WithMessage("Password must contain at least one special character");
        });
    }
}
