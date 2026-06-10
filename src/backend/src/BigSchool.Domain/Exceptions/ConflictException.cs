namespace BigSchool.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando hay un conflicto de negocio (ej: email duplicado, estado inválido).
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string errorCode, string message)
        : base(errorCode, message)
    {
    }
}
