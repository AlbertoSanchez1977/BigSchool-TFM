using BigSchool.Domain.Entities;

namespace BigSchool.Domain.Events;

public sealed record TransactionCreatedEvent(Transaction Transaction) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
