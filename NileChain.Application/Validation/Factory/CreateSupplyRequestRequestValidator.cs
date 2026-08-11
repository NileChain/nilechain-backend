using FluentValidation;
using NileChain.Application.Dtos.Factory;
using NileChain.Domain.Common;

namespace NileChain.Application.Validation.Factory;

public class CreateSupplyRequestRequestValidator : AbstractValidator<CreateSupplyRequestRequest>
{
    private static readonly string[] AllowedScopes = ["Exact", "Nearby", "Nationwide"];

    public CreateSupplyRequestRequestValidator()
    {
        RuleFor(x => x.Crop)
            .NotEmpty()
            .WithMessage("Crop is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than 0.")
            .LessThanOrEqualTo(99_999_999.99m)
            .WithMessage("Price must be at most 99,999,999.99 (decimal(10,2)).");

        // DeliveryDate is an Egypt calendar date; compare on UTC calendar day (grace = 0).
        // See DeliveryDatePolicy for storage at noon UTC.
        RuleFor(x => x.DeliveryDate)
            .NotEmpty()
            .WithMessage("Delivery date is required.")
            .Must(d => DeliveryDatePolicy.IsNotInPast(d))
            .WithMessage("Delivery date cannot be in the past.");

        RuleFor(x => x.GeographicScope)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || AllowedScopes.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("GeographicScope must be Exact, Nearby, or Nationwide.");
    }
}
