using FluentValidation;

namespace BigSchool.Application.Commands.Transactions.Update;

public class UpdateTransactionCommandValidator : AbstractValidator<UpdateTransactionCommand>
{
    public UpdateTransactionCommandValidator()
    {
        RuleFor(x => x.IdTransaction).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor que cero.");
        RuleFor(x => x.IdMainCategory).IsInEnum().WithMessage("La categoría no es válida.");
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.TransactionDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(255);
    }
}
