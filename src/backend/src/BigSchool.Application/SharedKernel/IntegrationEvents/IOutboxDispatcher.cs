namespace BigSchool.Application.SharedKernel.IntegrationEvents;

public interface IOutboxDispatcher
{
    Task DispatchPendingAsync(CancellationToken cancellationToken);
}
