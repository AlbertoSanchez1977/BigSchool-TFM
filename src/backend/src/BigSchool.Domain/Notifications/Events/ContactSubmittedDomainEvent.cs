using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Notifications.Events;

public sealed record ContactSubmittedDomainEvent(Contact Contact) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
