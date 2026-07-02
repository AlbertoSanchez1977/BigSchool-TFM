using FluentValidation;

namespace BigSchool.Application.Investments.Commands.CreateCompany;
public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Sector).IsInEnum();
        RuleFor(x => x.Market).IsInEnum();
    }
}
