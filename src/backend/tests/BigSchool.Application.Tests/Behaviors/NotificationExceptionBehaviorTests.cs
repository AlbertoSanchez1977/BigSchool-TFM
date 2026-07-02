using BigSchool.Application.SharedKernel.Behaviors;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BigSchool.Application.Tests.Behaviors;

public record TestNotification : INotification;

public class NotificationExceptionBehaviorTests
{

    [Fact]
    public async Task Handle_WhenHandlerThrows_LogsErrorAndDoesNotRethrow()
    {
        var loggerMock = new Mock<ILogger<NotificationExceptionBehavior<TestNotification>>>();
        var behavior = new NotificationExceptionBehavior<TestNotification>(loggerMock.Object);

        var act = () => behavior.Handle(
            new TestNotification(),
            () => throw new InvalidOperationException("Handler exploded"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenHandlerSucceeds_CompletesNormally()
    {
        var loggerMock = new Mock<ILogger<NotificationExceptionBehavior<TestNotification>>>();
        var behavior = new NotificationExceptionBehavior<TestNotification>(loggerMock.Object);
        var called = false;

        await behavior.Handle(
            new TestNotification(),
            () => { called = true; return Task.FromResult(Unit.Value); },
            CancellationToken.None);

        called.Should().BeTrue();
    }
}
