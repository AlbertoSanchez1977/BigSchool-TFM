namespace BigSchool.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando no se encuentra una entidad en el dominio.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base("ENTITY_NOT_FOUND", $"No se encontró {entityName} con id {key}.")
    {
    }
}
