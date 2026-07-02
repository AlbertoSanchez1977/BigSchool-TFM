using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Events;

public sealed record TransactionCreatedEvent(Transaction Transaction) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
