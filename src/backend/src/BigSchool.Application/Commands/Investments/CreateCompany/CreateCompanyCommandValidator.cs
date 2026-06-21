using FluentValidation;

namespace BigSchool.Application.Commands.Investments.CreateCompany;
public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Sector).MaximumLength(100);
        RuleFor(x => x.Market).MaximumLength(50);
    }
}
