using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Events;
using FluentAssertions;
using Xunit;

namespace BigSchool.Domain.Tests.Entities;

public class BaseEntityTests
{
    private class TestEntity : BaseEntity, IAggregateRoot
    {
    }

    private class TestEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddEventToCollection()
    {
        var entity = new TestEntity();
        var domainEvent = new TestEvent();

        entity.RaiseDomainEvent(domainEvent);

        entity.DomainEvents.Should().ContainSingle()
            .Which.Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        var entity = new TestEntity();
        entity.RaiseDomainEvent(new TestEvent());
        entity.RaiseDomainEvent(new TestEvent());

        entity.ClearDomainEvents();

        entity.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void NewEntity_ShouldHaveNoDomainEvents()
    {
        var entity = new TestEntity();

        entity.DomainEvents.Should().BeEmpty();
    }
}
