using MediatR;

namespace BigSchool.Application.Infrastructure;

public class CustomMediatR : Mediator
{
    private readonly IServiceProvider _serviceFactory;
    private readonly Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> _publish;
    private readonly Dictionary<PublishStrategy, IMediator> _publishStrategies;

    private CustomMediatR(IServiceProvider serviceFactory, Func<IEnumerable<NotificationHandlerExecutor>, INotification, CancellationToken, Task> publish) : base(serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _publish = publish;
        _publishStrategies = default!;
    }

    public CustomMediatR(IServiceProvider serviceFactory) : base(serviceFactory)
    {
        _serviceFactory = serviceFactory;
        _publish = base.PublishCore;

        _publishStrategies = new Dictionary<PublishStrategy, IMediator>
        {
            [PublishStrategy.Async] = new CustomMediatR(_serviceFactory, AsyncContinueOnException),
            [PublishStrategy.ParallelNoWait] = new CustomMediatR(_serviceFactory, ParallelNoWait),
            [PublishStrategy.ParallelWhenAll] = new CustomMediatR(_serviceFactory, ParallelWhenAll),
            [PublishStrategy.ParallelWhenAny] = new CustomMediatR(_serviceFactory, ParallelWhenAny),
            [PublishStrategy.SyncContinueOnException] = new CustomMediatR(_serviceFactory, SyncContinueOnException),
            [PublishStrategy.SyncStopOnException] = new CustomMediatR(_serviceFactory, SyncStopOnException)
        };
    }

    protected override Task PublishCore(IEnumerable<NotificationHandlerExecutor> allHandlers, INotification notification, CancellationToken cancellationToken)
    {
        return _publish(allHandlers, notification, cancellationToken);
    }

    public Task Publish<TNotification>(TNotification notification, PublishStrategy strategy, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (!_publishStrategies.TryGetValue(strategy, out var mediator))
        {
            throw new ArgumentException($"Unknown strategy: {strategy}");
        }

        return mediator.Publish(notification, cancellationToken);
    }

    private Task ParallelWhenAll(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            tasks.Add(Task.Run(() => handler.HandlerCallback(notification, cancellationToken)));
        }
        return Task.WhenAll(tasks);
    }

    private Task ParallelWhenAny(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        foreach (var handler in handlers)
        {
            tasks.Add(Task.Run(() => handler.HandlerCallback(notification, cancellationToken)));
        }
        return Task.WhenAny(tasks);
    }

    private Task ParallelNoWait(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            Task.Run(() => handler.HandlerCallback(notification, cancellationToken));
        }
        return Task.CompletedTask;
    }

    private async Task AsyncContinueOnException(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();
        var exceptions = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                tasks.Add(handler.HandlerCallback(notification, cancellationToken));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                exceptions.Add(ex);
            }
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (AggregateException ex)
        {
            exceptions.AddRange(ex.Flatten().InnerExceptions);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            exceptions.Add(ex);
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }

    private async Task SyncStopOnException(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        foreach (var handler in handlers)
        {
            await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncContinueOnException(IEnumerable<NotificationHandlerExecutor> handlers, INotification notification, CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandlerCallback(notification, cancellationToken).ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                exceptions.AddRange(ex.Flatten().InnerExceptions);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}

public enum PublishStrategy
{
    /// <summary>
    /// Run each notification handler after one another. Returns when all handlers are finished.
    /// In case of any exception(s), they will be captured in an AggregateException.
    /// </summary>
    SyncContinueOnException = 0,

    /// <summary>
    /// Run each notification handler after one another. Returns when all handlers are finished
    /// or an exception has been thrown. In case of an exception, any handlers after that will not be run.
    /// </summary>
    SyncStopOnException = 1,

    /// <summary>
    /// Run all notification handlers asynchronously. Returns when all handlers are finished.
    /// In case of any exception(s), they will be captured in an AggregateException.
    /// </summary>
    Async = 2,

    /// <summary>
    /// Run each notification handler on its own thread using Task.Run(). Returns immediately
    /// and does not wait for any handlers to finish.
    /// </summary>
    ParallelNoWait = 3,

    /// <summary>
    /// Run each notification handler on its own thread using Task.Run(). Returns when all
    /// threads (handlers) are finished.
    /// </summary>
    ParallelWhenAll = 4,

    /// <summary>
    /// Run each notification handler on its own thread using Task.Run(). Returns when any
    /// thread (handler) is finished.
    /// </summary>
    ParallelWhenAny = 5
}
