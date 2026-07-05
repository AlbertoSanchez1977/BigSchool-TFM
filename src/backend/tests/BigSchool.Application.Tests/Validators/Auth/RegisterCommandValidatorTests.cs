using BigSchool.Application.Auth.Commands.Register;
using BigSchool.Domain.SharedKernel.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Auth;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", Currency.EUR));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("", "P@ssw0rd!", "John Doe", Currency.EUR));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmailFormat_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("notanemail", "P@ssw0rd!", "John Doe", Currency.EUR));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_ShortPassword_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "short", "John Doe", Currency.EUR));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_EmptyFullName_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "", Currency.EUR));
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_PasswordExactly8Chars_NoError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "12345678", "John Doe", Currency.EUR));
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_ValidBaseCurrency_NoError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", Currency.USD));
        result.ShouldNotHaveValidationErrorFor(x => x.BaseCurrency);
    }

    [Fact]
    public void Validate_OmittedBaseCurrency_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", default));
        result.ShouldHaveValidationErrorFor(x => x.BaseCurrency);
    }

    [Fact]
    public void Validate_BaseCurrencyOutOfEnum_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe", (Currency)999));
        result.ShouldHaveValidationErrorFor(x => x.BaseCurrency);
    }
}
