using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(6)
            .Matches(@"^\d{6}$").WithMessage("OTP must be 6 digits");
    }
}
