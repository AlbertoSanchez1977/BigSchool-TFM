using System;
using BigSchool.Application.Investments.Commands.SellShares;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class SellSharesCommandValidatorTests
{
    private readonly SellSharesCommandValidator _validator = new();

    private static SellSharesCommand ValidCommand(DateOnly sellDate) =>
        new(PortfolioId: 1, IdUser: 1, IdCompany: 1, Shares: 5m, SellPrice: 120m, SellDate: sellDate, Notes: null);

    [Fact]
    public void Validate_SellDateToday_NoError()
        => _validator.TestValidate(ValidCommand(DateOnly.FromDateTime(DateTime.UtcNow)))
            .ShouldNotHaveValidationErrorFor(x => x.SellDate);

    [Fact]
    public void Validate_SellDateInFuture_HasError()
        => _validator.TestValidate(ValidCommand(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)))
            .ShouldHaveValidationErrorFor(x => x.SellDate);
}
