using FluentValidation;

namespace BigSchool.Application.Investments.Commands.RenamePortfolio;

public class RenamePortfolioCommandValidator : AbstractValidator<RenamePortfolioCommand>
{
    public RenamePortfolioCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}
