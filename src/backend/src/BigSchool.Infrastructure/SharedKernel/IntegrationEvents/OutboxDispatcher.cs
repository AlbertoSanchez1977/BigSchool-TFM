using System.Text.Json;
using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Drena el outbox post-commit (en proceso, mismo request). Evolución futura sin tocar contratos:
/// un BackgroundService que haga polling de esta misma tabla con este mismo bus/handlers.
/// </summary>
public sealed class OutboxDispatcher : IOutboxDispatcher
{
    private readonly BigSchoolDbContext _dbContext;
    private readonly IIntegrationEventBus _bus;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(BigSchoolDbContext dbContext, IIntegrationEventBus bus, ILogger<OutboxDispatcher> logger)
    {
        _dbContext = dbContext;
        _bus = bus;
        _logger = logger;
    }

    public async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        var pending = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedOn == null)
            .OrderBy(m => m.IdOutboxMessage)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            // Se marca y persiste ANTES de invocar el handler (no al final del foreach): si el handler
            // hace _mediator.Send(...) de un command cuyo propio OutboxDispatchBehavior reentra aquí
            // (p.ej. CreateWelcomeEmailCommand disparado por el welcome), la fila ya está ProcessedOn
            // en BD y la consulta de "pending" de la llamada anidada no la vuelve a traer. Sin esto la
            // fila sigue viéndose NULL en BD durante toda la reentrada → recursión infinita (comprobado:
            // 5000+ EmailLogs generados por un único registro antes de que el proceso quedara colgado).
            message.MarkProcessed(DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(dispatchEvents: false);

            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"No se pudo resolver el tipo de evento '{message.Type}'.");
                var @event = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await _bus.PublishAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando OutboxMessage {IdOutboxMessage} de tipo {Type}: {Message}",
                    message.IdOutboxMessage, message.Type, ex.Message);
                message.MarkFailed(DateTime.UtcNow, ex.Message);
                await _dbContext.SaveChangesAsync(dispatchEvents: false);
            }
        }
    }
}
