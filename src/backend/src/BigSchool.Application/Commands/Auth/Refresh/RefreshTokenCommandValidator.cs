using FluentValidation;

namespace BigSchool.Application.Commands.Auth.Refresh;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("El token de acceso es obligatorio.");
    }
}
