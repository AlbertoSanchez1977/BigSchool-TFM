using FluentValidation;

namespace BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;

public class GetCompanyValuationSeriesQueryValidator : AbstractValidator<GetCompanyValuationSeriesQuery>
{
    public GetCompanyValuationSeriesQueryValidator()
        => RuleFor(x => x.Period).IsInEnum()
            .WithMessage("El periodo debe ser uno de: ThreeMonths, SixMonths, OneYear, ThreeYears, FiveYears.");
}
