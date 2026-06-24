namespace BigSchool.Domain.Exceptions;

public class EmailAlreadyExistsDomainException : ConflictException
{
    public EmailAlreadyExistsDomainException(string email)
        : base("EMAIL_ALREADY_EXISTS", $"Ya existe un usuario registrado con el email '{email}'.") { }
}
