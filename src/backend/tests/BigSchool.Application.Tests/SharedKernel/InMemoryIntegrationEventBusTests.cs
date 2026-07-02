using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BigSchool.Application.Tests.SharedKernel;

public class InMemoryIntegrationEventBusTests
{
    private sealed record TestEvent(Guid EventId, DateTime OccurredOn, string Payload) : IIntegrationEvent;

    private sealed class TestHandler : IIntegrationEventHandler<TestEvent>
    {
        public List<TestEvent> Received { get; } = new();
        public Task HandleAsync(TestEvent @event, CancellationToken ct)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishAsync_invoca_el_handler_registrado_para_el_tipo_concreto()
    {
        var handler = new TestHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<TestEvent>>(handler);
        var provider = services.BuildServiceProvider();
        var bus = new InMemoryIntegrationEventBus(provider);
        var evt = new TestEvent(Guid.NewGuid(), DateTime.UtcNow, "hola");

        await bus.PublishAsync(evt, CancellationToken.None);

        handler.Received.Should().ContainSingle().Which.Payload.Should().Be("hola");
    }

    [Fact]
    public async Task PublishAsync_sin_handlers_es_noop()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var bus = new InMemoryIntegrationEventBus(provider);
        var evt = new TestEvent(Guid.NewGuid(), DateTime.UtcNow, "x");

        var act = async () => await bus.PublishAsync(evt, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
