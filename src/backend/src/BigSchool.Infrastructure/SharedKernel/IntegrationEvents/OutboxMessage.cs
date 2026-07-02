namespace BigSchool.Infrastructure.SharedKernel.IntegrationEvents;

/// <summary>
/// Fila del outbox. Infraestructura pura (no AR de negocio): sin IdStatus ni Global Query Filter,
/// igual que ExchangeRate. Se inserta en la MISMA transacción que el agregado.
/// </summary>
public class OutboxMessage
{
    public long IdOutboxMessage { get; private set; }
    public Guid EventId { get; private set; }
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTime OccurredOn { get; private set; }
    public DateTime? ProcessedOn { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage(Guid eventId, string type, string payload, DateTime occurredOn)
    {
        EventId = eventId;
        Type = type;
        Payload = payload;
        OccurredOn = occurredOn;
    }

    protected OutboxMessage() { } // EF Core

    public static OutboxMessage Create(Guid eventId, string type, string payload, DateTime occurredOn)
        => new(eventId, type, payload, occurredOn);

    public void MarkProcessed(DateTime processedOn) => ProcessedOn = processedOn;
    public void MarkFailed(DateTime processedOn, string error)
    {
        ProcessedOn = processedOn;
        Error = error;
    }
}
