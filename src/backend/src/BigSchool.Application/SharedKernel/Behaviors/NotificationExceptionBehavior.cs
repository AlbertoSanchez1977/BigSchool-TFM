using MediatR;
using Microsoft.Extensions.Logging;

namespace BigSchool.Application.SharedKernel.Behaviors;

/// <summary>
/// Envuelve todos los INotificationHandler con try-catch para evitar que
/// excepciones en handlers de DomainEvents/IntegrationEvents revienten la app.
/// </summary>
public class NotificationExceptionBehavior<TNotification> : IPipelineBehavior<TNotification, Unit>
    where TNotification : INotification
{
    private readonly ILogger<NotificationExceptionBehavior<TNotification>> _logger;

    public NotificationExceptionBehavior(ILogger<NotificationExceptionBehavior<TNotification>> logger)
    {
        _logger = logger;
    }

    public async Task<Unit> Handle(
        TNotification notification,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error en handler de notification {NotificationType}: {Message}",
                typeof(TNotification).Name, ex.Message);
        }

        return Unit.Value;
    }
}
