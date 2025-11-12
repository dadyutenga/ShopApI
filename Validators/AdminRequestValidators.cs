using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

public class RegisterManagerRequestValidator : AbstractValidator<RegisterManagerRequest>
{
    public RegisterManagerRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8);
    }
}

public class RegisterSupportRequestValidator : AbstractValidator<RegisterSupportRequest>
{
    public RegisterSupportRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8);
    }
}

public class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).MaximumLength(20).When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.IsActive).NotNull();
    }
}
