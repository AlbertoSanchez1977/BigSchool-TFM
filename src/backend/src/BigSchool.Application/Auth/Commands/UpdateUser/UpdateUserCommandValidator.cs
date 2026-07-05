using FluentValidation;

namespace BigSchool.Application.Auth.Commands.UpdateUser;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        When(x => x.Password is not null, () =>
            RuleFor(x => x.Password!).MinimumLength(8).MaximumLength(100));
    }
}
