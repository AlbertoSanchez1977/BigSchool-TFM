using System;
using FluentValidation;

namespace BigSchool.Application.Investments.Commands.SellShares;

public class SellSharesCommandValidator : AbstractValidator<SellSharesCommand>
{
    public SellSharesCommandValidator()
    {
        RuleFor(x => x.IdCompany).GreaterThan(0);
        RuleFor(x => x.Shares).GreaterThan(0m);
        RuleFor(x => x.SellPrice).GreaterThan(0m);
        RuleFor(x => x.SellDate).NotEmpty();
        RuleFor(x => x.SellDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de venta no puede ser futura.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
