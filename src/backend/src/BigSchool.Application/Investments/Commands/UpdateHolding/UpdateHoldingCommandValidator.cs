using FluentValidation;

namespace BigSchool.Application.Investments.Commands.UpdateHolding;

public class UpdateHoldingCommandValidator : AbstractValidator<UpdateHoldingCommand>
{
    public UpdateHoldingCommandValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
