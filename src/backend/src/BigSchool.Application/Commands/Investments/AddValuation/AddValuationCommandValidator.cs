using FluentValidation;

namespace BigSchool.Application.Commands.Investments.AddValuation;

public class AddValuationCommandValidator : AbstractValidator<AddValuationCommand>
{
    public AddValuationCommandValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("El precio debe ser mayor que cero.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("La fecha es obligatoria.");
        RuleFor(x => x.Source).MaximumLength(100);
    }
}
