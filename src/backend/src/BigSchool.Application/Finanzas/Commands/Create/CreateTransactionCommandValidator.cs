using FluentValidation;

namespace BigSchool.Application.Finanzas.Commands.Create;

public class CreateTransactionCommandValidator : AbstractValidator<CreateTransactionCommand>
{
    public CreateTransactionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El importe debe ser mayor que cero.");

        RuleFor(x => x.IdMainCategory)
            .IsInEnum().WithMessage("La categoría no es válida.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de transacción no es válido.");

        RuleFor(x => x.TransactionDate)
            .NotEmpty().WithMessage("La fecha es obligatoria.");

        RuleFor(x => x.Description)
            .MaximumLength(255);
    }
}
