using BigSchool.Domain.SharedKernel.Events;
using MediatR;

namespace BigSchool.Application.Events;

public class DomainEventNotification<T> : INotification where T : IDomainEvent
{
    public T DomainEvent { get; }

    public DomainEventNotification(T domainEvent)
    {
        DomainEvent = domainEvent;
    }
}
