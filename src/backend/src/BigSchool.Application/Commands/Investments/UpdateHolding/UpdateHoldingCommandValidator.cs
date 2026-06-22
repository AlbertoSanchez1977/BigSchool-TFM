using FluentValidation;

namespace BigSchool.Application.Commands.Investments.UpdateHolding;

public class UpdateHoldingCommandValidator : AbstractValidator<UpdateHoldingCommand>
{
    public UpdateHoldingCommandValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
