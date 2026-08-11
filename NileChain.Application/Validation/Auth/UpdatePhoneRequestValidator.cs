using FluentValidation;
using NileChain.Application.Dtos.Auth.Requests;
using NileChain.Application.Validation.Common;

namespace NileChain.Application.Validation.Auth;

public class UpdatePhoneRequestValidator : AbstractValidator<UpdatePhoneRequest>
{
    public UpdatePhoneRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(EgyptianPhone.InvalidMessage)
            .Must(EgyptianPhone.IsValid)
            .WithMessage(EgyptianPhone.InvalidMessage);
    }
}
