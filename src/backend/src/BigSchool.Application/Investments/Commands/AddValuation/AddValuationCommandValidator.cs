using System;
using FluentValidation;

namespace BigSchool.Application.Investments.Commands.AddValuation;

public class AddValuationCommandValidator : AbstractValidator<AddValuationCommand>
{
    public AddValuationCommandValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("El precio debe ser mayor que cero.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("La fecha es obligatoria.");
        RuleFor(x => x.Date)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("La fecha de la valoración no puede ser futura.");
        RuleFor(x => x.Source).MaximumLength(100);
    }
}
