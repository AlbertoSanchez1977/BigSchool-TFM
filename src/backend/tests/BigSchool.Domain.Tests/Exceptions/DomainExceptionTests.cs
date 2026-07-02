using BigSchool.Domain.Exceptions;
using BigSchool.Domain.SharedKernel.Exceptions;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Exceptions;

public class DomainExceptionTests
{
    [Fact]
    public void NotFoundException_HasCodeAndEntityInfo()
    {
        var ex = new NotFoundException("User", 42);

        ex.Message.Should().Contain("User");
        ex.Message.Should().Contain("42");
        ex.ErrorCode.Should().Be("ENTITY_NOT_FOUND");
    }

    [Fact]
    public void ConflictException_HasCodeAndMessage()
    {
        var ex = new ConflictException("EMAIL_ALREADY_EXISTS", "Ya existe un usuario con ese email.");

        ex.Message.Should().Be("Ya existe un usuario con ese email.");
        ex.ErrorCode.Should().Be("EMAIL_ALREADY_EXISTS");
    }

    [Fact]
    public void InvalidCredentialsDomainException_HasSpecificCode()
    {
        var ex = new InvalidCredentialsDomainException();

        ex.ErrorCode.Should().Be("INVALID_CREDENTIALS");
        ex.Message.Should().Contain("inválidas");
    }

    [Fact]
    public void DomainException_IsAbstract_CannotBeInstantiatedDirectly()
    {
        typeof(DomainException).IsAbstract.Should().BeTrue();
    }
}
