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

            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.BankName).MaximumLength(120);
            RuleFor(x => x.AccountHolderName).MaximumLength(120);
            RuleFor(x => x.BankAccountNumber).MaximumLength(64);
            RuleFor(x => x.Iban).MaximumLength(34);
        }
    }
}
