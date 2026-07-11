using BigSchool.Application.Investments.Commands.RenamePortfolio;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Investments;

public class RenamePortfolioCommandValidatorTests
{
    private readonly RenamePortfolioCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidName_NoError()
        => _validator.TestValidate(new RenamePortfolioCommand(1, 1, "Nueva cartera")).ShouldNotHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Validate_EmptyName_HasError()
        => _validator.TestValidate(new RenamePortfolioCommand(1, 1, "")).ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Validate_NameTooLong_HasError()
        => _validator.TestValidate(new RenamePortfolioCommand(1, 1, new string('A', 101))).ShouldHaveValidationErrorFor(x => x.Name);
}
