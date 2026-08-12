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

        RuleFor(x => x.DeliveryPoint)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || DeliveryTermsPolicy.TryParsePoint(s, out _))
            .WithMessage("DeliveryPoint must be FarmGate or FactoryGate.");

        RuleFor(x => x.FreightPayer)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || DeliveryTermsPolicy.TryParseParty(s, out _))
            .WithMessage("FreightPayer must be Farm or Factory.");

        RuleFor(x => x.TransitRisk)
            .Must(s => string.IsNullOrWhiteSpace(s)
                || DeliveryTermsPolicy.TryParseParty(s, out _))
            .WithMessage("TransitRisk must be Farm or Factory.");

        When(x => x.StructuredQuality is not null, () =>
        {
            RuleFor(x => x.StructuredQuality!.MoistureMaxPercent)
                .InclusiveBetween(0, 100)
                .When(x => x.StructuredQuality!.MoistureMaxPercent is not null);

            RuleFor(x => x.StructuredQuality!.ImpuritiesMaxPercent)
                .InclusiveBetween(0, 100)
                .When(x => x.StructuredQuality!.ImpuritiesMaxPercent is not null);

            RuleFor(x => x.StructuredQuality!.Grade)
                .MaximumLength(40)
                .When(x => !string.IsNullOrWhiteSpace(x.StructuredQuality!.Grade));
        });
    }
}
