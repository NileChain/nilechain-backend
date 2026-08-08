using FluentValidation;
using NileChain.Application.Dtos.Factory;

namespace NileChain.Application.Validation.Factory;

public class CreateSupplyRequestRequestValidator : AbstractValidator<CreateSupplyRequestRequest>
{
    public CreateSupplyRequestRequestValidator()
    {
        RuleFor(x => x.Crop)
            .NotEmpty()
            .WithMessage("Crop is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price must be 0 or greater.");

        RuleFor(x => x.DeliveryDate)
            .NotEmpty()
            .WithMessage("Delivery date is required.");
    }
}
