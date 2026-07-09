using System;
using FluentValidation;

namespace BigSchool.Application.Investments.Commands.AddHolding;

public class AddHoldingCommandValidator : AbstractValidator<AddHoldingCommand>
{
    public AddHoldingCommandValidator()
    {
        RuleFor(x => x.IdCompany).GreaterThan(0);
        RuleFor(x => x.Shares).GreaterThan(0m);
        RuleFor(x => x.BuyPrice).GreaterThan(0m);
        RuleFor(x => x.BuyDate).NotEmpty();
        RuleFor(x => x.BuyDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de compra no puede ser futura.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
