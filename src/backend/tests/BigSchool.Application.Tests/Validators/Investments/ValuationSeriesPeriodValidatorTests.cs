using BigSchool.Application.Investments.Queries.GetCompanyValuationSeries;
using BigSchool.Domain.Investments.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class ValuationSeriesPeriodValidatorTests
{
    private readonly GetCompanyValuationSeriesQueryValidator _validator = new();

    [Theory]
    [InlineData(ValuationPeriod.ThreeMonths)]
    [InlineData(ValuationPeriod.OneYear)]
    [InlineData(ValuationPeriod.FiveYears)]
    public void Validate_ValidPeriod_NoError(ValuationPeriod p)
        => _validator.TestValidate(new GetCompanyValuationSeriesQuery(1, p)).ShouldNotHaveValidationErrorFor(x => x.Period);

    [Fact]
    public void Validate_PeriodOutOfEnum_HasError()
        => _validator.TestValidate(new GetCompanyValuationSeriesQuery(1, (ValuationPeriod)99)).ShouldHaveValidationErrorFor(x => x.Period);
}
