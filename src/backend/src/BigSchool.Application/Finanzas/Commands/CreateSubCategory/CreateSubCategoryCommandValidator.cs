using FluentValidation;

namespace BigSchool.Application.Finanzas.Commands.CreateSubCategory;

public class CreateSubCategoryCommandValidator : AbstractValidator<CreateSubCategoryCommand>
{
    public CreateSubCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MainCategory).IsInEnum();
    }
}
