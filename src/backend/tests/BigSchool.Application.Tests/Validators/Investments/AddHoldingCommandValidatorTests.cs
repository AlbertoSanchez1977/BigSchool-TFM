using System;
using BigSchool.Application.Investments.Commands.AddHolding;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class AddHoldingCommandValidatorTests
{
    private readonly AddHoldingCommandValidator _validator = new();

    private static AddHoldingCommand ValidCommand(DateOnly buyDate) =>
        new(PortfolioId: 1, IdUser: 1, IdCompany: 1, Shares: 10m, BuyPrice: 100m, BuyDate: buyDate, Notes: null);

    [Fact]
    public void Validate_BuyDateToday_NoError()
        => _validator.TestValidate(ValidCommand(DateOnly.FromDateTime(DateTime.UtcNow)))
            .ShouldNotHaveValidationErrorFor(x => x.BuyDate);

    [Fact]
    public void Validate_BuyDateInFuture_HasError()
        => _validator.TestValidate(ValidCommand(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)))
            .ShouldHaveValidationErrorFor(x => x.BuyDate);
}
