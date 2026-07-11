using BigSchool.Domain.SharedKernel.Exceptions;

namespace BigSchool.Domain.Auth.Exceptions;

public class EmailAlreadyExistsDomainException : ConflictException
{
    public EmailAlreadyExistsDomainException(string email)
        : base("EMAIL_ALREADY_EXISTS", $"Ya existe un usuario registrado con el email '{email}'.") { }
}
