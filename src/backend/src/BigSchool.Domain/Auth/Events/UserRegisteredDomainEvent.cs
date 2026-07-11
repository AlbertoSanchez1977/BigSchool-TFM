using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Auth.Events;

public sealed record UserRegisteredDomainEvent(User User) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
