using System;
using BigSchool.Application.Investments.Commands.AddValuation;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class AddValuationCommandValidatorTests
{
    private readonly AddValuationCommandValidator _validator = new();

    private static AddValuationCommand ValidCommand(DateOnly date) =>
        new(IdCompany: 1, Price: 190.5m, Date: date, Source: null);

    [Fact]
    public void Validate_DateToday_NoError()
        => _validator.TestValidate(ValidCommand(DateOnly.FromDateTime(DateTime.UtcNow)))
            .ShouldNotHaveValidationErrorFor(x => x.Date);

    [Fact]
    public void Validate_DateInFuture_HasError()
        => _validator.TestValidate(ValidCommand(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)))
            .ShouldHaveValidationErrorFor(x => x.Date);
}
