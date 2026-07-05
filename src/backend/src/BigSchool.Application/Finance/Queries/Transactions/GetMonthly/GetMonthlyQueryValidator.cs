using BigSchool.Application.SharedKernel.Common;
using FluentValidation;

namespace BigSchool.Application.Finance.Queries.Transactions.GetMonthly;

public class GetMonthlyQueryValidator : AbstractValidator<GetMonthlyQuery>
{
    public GetMonthlyQueryValidator()
    {
        RuleFor(x => x).Must(q => DateRange.IsValid(q.From, q.To))
            .WithName("dateRange")
            .WithMessage("El rango de fechas es inválido (from ≤ to y máximo 4 años).");
    }
}
