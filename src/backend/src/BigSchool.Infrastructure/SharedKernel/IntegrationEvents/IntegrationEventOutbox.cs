using System.Text.Json;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

public sealed class IntegrationEventOutbox : IIntegrationEventOutbox
{
    private readonly BigSchoolDbContext _dbContext;

    public IntegrationEventOutbox(BigSchoolDbContext dbContext) => _dbContext = dbContext;

    public void Add(IIntegrationEvent @event)
    {
        var type = @event.GetType().AssemblyQualifiedName!;
        var payload = JsonSerializer.Serialize(@event, @event.GetType());
        _dbContext.OutboxMessages.Add(
            OutboxMessage.Create(@event.EventId, type, payload, @event.OccurredOn));
    }
}
