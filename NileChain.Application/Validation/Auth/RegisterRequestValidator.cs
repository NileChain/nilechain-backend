using NileChain.Application.Dtos.Auth.Requests;
using NileChain.Application.Validation.Common;
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

            RuleFor(x => x.Phone)
                .NotEmpty()
                .Must(EgyptianPhone.IsValid)
                .WithMessage(EgyptianPhone.InvalidMessage);

            RuleFor(x => x.BusinessType)
                .NotEmpty()
                .Must(x =>
                    x.Equals("Farm", StringComparison.OrdinalIgnoreCase)
                    || x.Equals("Factory", StringComparison.OrdinalIgnoreCase))
                .WithMessage("BusinessType must be Farm or Factory.");

            When(x => IsFarm(x.BusinessType), () =>
            {
                RuleFor(x => x.Name)
                    .NotEmpty()
                    .WithMessage("Farm name is required.");
                RuleFor(x => x.Governorate)
                    .NotEmpty()
                    .WithMessage("Governorate is required.");
                RuleFor(x => x.SizeInFeddans)
                    .NotNull()
                    .WithMessage("Size in feddans is required.")
                    .GreaterThan(0)
                    .WithMessage("Size in feddans must be greater than 0.");
            });

            When(x => IsFactory(x.BusinessType), () =>
            {
                RuleFor(x => x.Name)
                    .NotEmpty()
                    .WithMessage("Factory name is required.");
                RuleFor(x => x.Governorate)
                    .NotEmpty()
                    .WithMessage("Governorate is required.");
            });
        }

        private static bool IsFarm(string? businessType) =>
            !string.IsNullOrWhiteSpace(businessType)
            && businessType.Equals("Farm", StringComparison.OrdinalIgnoreCase);

        private static bool IsFactory(string? businessType) =>
            !string.IsNullOrWhiteSpace(businessType)
            && businessType.Equals("Factory", StringComparison.OrdinalIgnoreCase);
    }
}
