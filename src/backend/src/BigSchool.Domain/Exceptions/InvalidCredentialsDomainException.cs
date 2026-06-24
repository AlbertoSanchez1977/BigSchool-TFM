namespace BigSchool.Domain.Exceptions;

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
