using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Auth.Events;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities.Auth;

public class UserRegistrationEventTests
{
    [Fact]
    public void Create_levanta_UserRegisteredDomainEvent_con_la_entidad()
    {
        var u = User.Create("ada@example.com", "hash", "salt", "Ada");
        u.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserRegisteredDomainEvent>();
        ((UserRegisteredDomainEvent)u.DomainEvents.Single()).User.Should().BeSameAs(u);
    }
}
