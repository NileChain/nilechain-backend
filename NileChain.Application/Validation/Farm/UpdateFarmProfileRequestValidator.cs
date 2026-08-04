using NileChain.Application.Dtos.Farm;
using FluentValidation;

namespace NileChain.Application.Validation.Farm
{
    public class UpdateFarmProfileRequestValidator : AbstractValidator<UpdateFarmProfileRequest>
    {
        public UpdateFarmProfileRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Farm name is required.");

            RuleFor(x => x.Governorate)
                .NotEmpty()
                .WithMessage("Governorate is required.");
        }
    }
}
