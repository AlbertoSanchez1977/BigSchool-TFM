using BigSchool.Application.Notifications.Commands.CreateContact;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Notifications;

public class CreateContactCommandValidatorTests
{
    private readonly CreateContactCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var result = _validator.TestValidate(new CreateContactCommand("Ada", "ada@example.com", "Hola"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyFullName_HasError()
    {
        var result = _validator.TestValidate(new CreateContactCommand("", "ada@example.com", "Hola"));
        result.ShouldHaveValidationErrorFor(x => x.FullName);
    }

    [Fact]
    public void Validate_InvalidEmail_HasError()
    {
        var result = _validator.TestValidate(new CreateContactCommand("Ada", "no-email", "Hola"));
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Validate_EmptyMessage_HasError()
    {
        var result = _validator.TestValidate(new CreateContactCommand("Ada", "ada@example.com", ""));
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }
}
