using BigSchool.Application.Commands.Auth.Register;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", "John Doe"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("", "P@ssw0rd!", "John Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmailFormat_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("notanemail", "P@ssw0rd!", "John Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_ShortPassword_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "short", "John Doe"));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_EmptyFullName_HasError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "P@ssw0rd!", ""));
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_PasswordExactly8Chars_NoError()
    {
        var result = _validator.TestValidate(new RegisterCommand("test@email.com", "12345678", "John Doe"));
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
