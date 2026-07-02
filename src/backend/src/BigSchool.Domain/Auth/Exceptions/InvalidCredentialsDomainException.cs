using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Auth.Exceptions;

/// <summary>
/// Excepción lanzada cuando las credenciales de login son inválidas.
/// </summary>
public class InvalidCredentialsDomainException : DomainException
{
    public InvalidCredentialsDomainException()
        : base("INVALID_CREDENTIALS", "Las credenciales proporcionadas son inválidas.")
    {
    }
}
