using System.Text.Json;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Drena el outbox post-commit (en proceso, mismo request). Evolución futura sin tocar contratos:
/// un BackgroundService que haga polling de esta misma tabla con este mismo bus/handlers.
/// </summary>
public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly BigSchoolDbContext _dbContext;
    private readonly IIntegrationEventBus _bus;

    public OutboxDispatcher(BigSchoolDbContext dbContext, IIntegrationEventBus bus)
    {
        _dbContext = dbContext;
        _bus = bus;
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.IdOutboxMessage)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"No se pudo resolver el tipo de evento '{message.Type}'.");
                var @event = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await _bus.PublishAsync(@event, cancellationToken);
                message.MarkProcessed(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                message.MarkFailed(DateTime.UtcNow, ex.Message);
            }
        }

        await _dbContext.SaveChangesAsync(dispatchEvents: false);
    }
}
