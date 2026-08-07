using NileChain.Application.Dtos.Factory;
using FluentValidation;

namespace NileChain.Application.Validation.Factory
{
    public class UpdateFactoryProfileRequestValidator : AbstractValidator<UpdateFactoryProfileRequest>
    {
        public UpdateFactoryProfileRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Factory name is required.");

            RuleFor(x => x.Governorate)
                .NotEmpty()
                .WithMessage("Governorate is required.");
        }
    }
}
