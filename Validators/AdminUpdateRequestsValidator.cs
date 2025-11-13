using FluentValidation;
using ShopApI.DTOs;

namespace ShopApI.Validators;

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

public class SoftDeleteUserRequestValidator : AbstractValidator<SoftDeleteUserRequest>
{
    public SoftDeleteUserRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
