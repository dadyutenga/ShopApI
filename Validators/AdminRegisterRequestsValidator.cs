using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class AdminRegisterManagerRequestValidator : AbstractValidator<AdminRegisterManagerRequest>
{
    public AdminRegisterManagerRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12);
    }
}

public class AdminRegisterSupportRequestValidator : AbstractValidator<AdminRegisterSupportRequest>
{
    public AdminRegisterSupportRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12);
    }
}

public class AdminRegisterCustomerRequestValidator : AbstractValidator<AdminRegisterCustomerRequest>
{
    public AdminRegisterCustomerRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).MaximumLength(32).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
