using BigSchool.Application.Auth.Commands.UpdateUser;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Auth;

public class UpdateUserCommandValidatorTests
{
    private readonly UpdateUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommandWithoutPassword_NoErrors()
        => _validator.TestValidate(new UpdateUserCommand(1, "Ada", null)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_EmptyFullName_HasError()
        => _validator.TestValidate(new UpdateUserCommand(1, "", null)).ShouldHaveValidationErrorFor(x => x.FullName);

    [Fact]
    public void Validate_ShortPassword_HasError()
        => _validator.TestValidate(new UpdateUserCommand(1, "Ada", "123")).ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Validate_ValidCommandWithPassword_NoErrors()
        => _validator.TestValidate(new UpdateUserCommand(1, "Ada", "Secret123!")).ShouldNotHaveAnyValidationErrors();
}
