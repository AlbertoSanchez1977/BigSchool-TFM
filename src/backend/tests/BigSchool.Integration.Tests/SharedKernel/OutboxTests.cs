using BigSchool.Application.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using BigSchool.Infrastructure.SharedKernel.Persistence;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BigSchool.Integration.Tests.SharedKernel;

[Collection(IntegrationCollection.Name)]
public class OutboxTests
{
    private readonly MySqlDatabaseFixture _db;
    public OutboxTests(MySqlDatabaseFixture db) => _db = db;

    private sealed record DummyEvent(Guid EventId, DateTime OccurredOn) : IIntegrationEvent;

    private BigSchoolDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<BigSchoolDbContext>()
            .UseMySql(_db.ConnectionString, ServerVersion.AutoDetect(_db.ConnectionString))
            .Options;
        return new BigSchoolDbContext(options, Mock.Of<IMediator>());
    }

    [Fact]
    public async Task Outbox_persiste_la_fila_en_el_mismo_SaveChanges()
    {
        await _db.ResetAsync();
        await using var ctx = NewContext();
        new IntegrationEventOutbox(ctx).Add(new DummyEvent(Guid.NewGuid(), DateTime.UtcNow));
        await ctx.SaveChangesAsync(dispatchEvents: false);

        var stored = await ctx.OutboxMessages.SingleAsync();
        stored.ProcessedOn.Should().BeNull();
    }

    [Fact]
    public async Task Dispatcher_drena_publica_una_vez_y_marca_ProcessedOn()
    {
        await _db.ResetAsync();
        var evt = new DummyEvent(Guid.NewGuid(), DateTime.UtcNow);
        await using (var ctx = NewContext())
        {
            new IntegrationEventOutbox(ctx).Add(evt);
            await ctx.SaveChangesAsync(dispatchEvents: false);
        }

        var busMock = new Mock<IIntegrationEventBus>();
        await using (var ctx = NewContext())
            await new OutboxDispatcher(ctx, busMock.Object).DispatchPendingAsync(CancellationToken.None);

        busMock.Verify(b => b.PublishAsync(
            It.Is<IIntegrationEvent>(e => e.EventId == evt.EventId), It.IsAny<CancellationToken>()),
            Times.Once);

        await using (var ctx = NewContext())
            (await ctx.OutboxMessages.SingleAsync()).ProcessedOn.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispatcher_no_reprocesa_filas_ya_marcadas()
    {
        await _db.ResetAsync();
        await using (var ctx = NewContext())
        {
            new IntegrationEventOutbox(ctx).Add(new DummyEvent(Guid.NewGuid(), DateTime.UtcNow));
            await ctx.SaveChangesAsync(dispatchEvents: false);
        }
        var busMock = new Mock<IIntegrationEventBus>();
        await using (var ctx = NewContext())
            await new OutboxDispatcher(ctx, busMock.Object).DispatchPendingAsync(CancellationToken.None);
        await using (var ctx = NewContext())
            await new OutboxDispatcher(ctx, busMock.Object).DispatchPendingAsync(CancellationToken.None);

        busMock.Verify(b => b.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
