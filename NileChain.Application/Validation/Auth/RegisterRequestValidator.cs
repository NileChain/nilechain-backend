using NileChain.Application.Dtos.Auth.Requests;
using FluentValidation;

namespace NileChain.Application.Validation.Auth
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Matches("[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter.")
                .Matches("[a-z]")
                .WithMessage("Password must contain at least one lowercase letter.")
                .Matches("[0-9]")
                .WithMessage("Password must contain at least one number.");

            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.Password)
                .WithMessage("Passwords do not match.");

            RuleFor(x => x.BusinessType)
                .Must(x =>
                    x.Equals("Farm", StringComparison.OrdinalIgnoreCase)
                    || x.Equals("Factory", StringComparison.OrdinalIgnoreCase))
                .WithMessage("BusinessType must be Farm or Factory.");

            When(x => x.BusinessType.Equals("Farm", StringComparison.OrdinalIgnoreCase), () =>
            {
                RuleFor(x => x.Name)
                    .NotEmpty()
                    .WithMessage("Farm name is required.");
                RuleFor(x => x.Governorate)
                    .NotEmpty()
                    .WithMessage("Governorate is required.");
                RuleFor(x => x.SizeInFeddans)
                    .NotNull()
                    .GreaterThan(0)
                    .WithMessage("Size in feddans must be greater than 0.");
            });
        }
    }
}
