using BigSchool.Application.Auth.Commands.Login;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Auth;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(new LoginCommand("user@test.com", "password"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyEmail_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_InvalidEmail_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("notvalid", "password"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptyPassword_HasError()
    {
        var result = _validator.TestValidate(new LoginCommand("user@test.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
