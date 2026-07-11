using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.Notifications.Events;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Notifications;

public class ContactTests
{
    [Fact]
    public void Create_valido_asigna_campos_y_levanta_ContactSubmitted()
    {
        var c = Contact.Create("Ada Lovelace", "ADA@Example.com", " Hola ");
        c.FullName.Should().Be("Ada Lovelace");
        c.Email.Should().Be("ada@example.com");
        c.Message.Should().Be("Hola");
        c.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ContactSubmittedDomainEvent>();
    }

    [Theory]
    [InlineData("", "a@b.com", "m")]
    [InlineData("Ada", "sin-arroba", "m")]
    [InlineData("Ada", "a@b.com", "")]
    public void Create_invalido_lanza(string name, string email, string msg)
        => FluentActions.Invoking(() => Contact.Create(name, email, msg)).Should().Throw<ArgumentException>();
}
