using BigSchool.Application.Finance.Commands.CreateSubCategory;
using BigSchool.Domain.Finance.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace BigSchool.Application.Tests.Validators.Finance;

public class CreateSubCategoryCommandValidatorTests
{
    private readonly CreateSubCategoryCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
        => _validator.TestValidate(new CreateSubCategoryCommand(1, MainCategory.Luxuries, "Cine")).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_EmptyName_HasError()
        => _validator.TestValidate(new CreateSubCategoryCommand(1, MainCategory.Luxuries, "")).ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Validate_NameTooLong_HasError()
        => _validator.TestValidate(new CreateSubCategoryCommand(1, MainCategory.Luxuries, new string('a', 101))).ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Validate_MainCategoryOutOfEnum_HasError()
        => _validator.TestValidate(new CreateSubCategoryCommand(1, (MainCategory)999, "Cine")).ShouldHaveValidationErrorFor(x => x.MainCategory);
}
