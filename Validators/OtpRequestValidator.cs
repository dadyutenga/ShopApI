using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class OtpRequestValidator : AbstractValidator<OtpRequest>
{
    public OtpRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);
    }
}

public class OtpVerifyRequestValidator : AbstractValidator<OtpVerifyRequest>
{
    public OtpVerifyRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Otp)
            .NotEmpty()
            .Length(6)
            .Matches("^\\d{6}$");
    }
}
