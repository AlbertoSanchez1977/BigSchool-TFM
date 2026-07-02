using BigSchool.Application.SharedKernel.IntegrationEvents;
using MediatR;

namespace BigSchool.Application.SharedKernel.Behaviors;

/// <summary>
/// Tras completar un command (que ya hizo SaveChanges de agregado+outbox atómicamente),
/// drena el outbox en el mismo request. Registrado como pipeline behavior de MediatR.
/// </summary>
public sealed class OutboxDispatchBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IOutboxDispatcher _dispatcher;

    public OutboxDispatchBehavior(IOutboxDispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        await _dispatcher.DispatchPendingAsync(cancellationToken);
        return response;
    }
}
