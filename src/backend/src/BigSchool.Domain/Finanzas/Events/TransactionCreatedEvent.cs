using BigSchool.Domain.Finanzas.Entities;
using BigSchool.Domain.SharedKernel.Events;

namespace BigSchool.Domain.Finanzas.Events;

public sealed record TransactionCreatedEvent(Transaction Transaction) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
