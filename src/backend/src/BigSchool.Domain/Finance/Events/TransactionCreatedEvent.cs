using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Finance.Events;

public sealed record TransactionCreatedEvent(Transaction Transaction) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
