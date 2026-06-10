using BigSchool.Application.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;

namespace BigSchool.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    private record TestRequest(string Name) : IRequest<string>;

    private class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name es obligatorio.");
        }
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNext()
    {
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var called = false;

        var result = await behavior.Handle(
            new TestRequest("Valid"),
            () => { called = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        called.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationException()
    {
        var validators = new List<IValidator<TestRequest>> { new TestRequestValidator() };
        var behavior = new ValidationBehavior<TestRequest, string>(validators);

        var act = () => behavior.Handle(
            new TestRequest(""),
            () => Task.FromResult("should not reach"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.ErrorMessage.Contains("obligatorio")));
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        var validators = new List<IValidator<TestRequest>>();
        var behavior = new ValidationBehavior<TestRequest, string>(validators);
        var called = false;

        await behavior.Handle(
            new TestRequest("anything"),
            () => { called = true; return Task.FromResult("ok"); },
            CancellationToken.None);

        called.Should().BeTrue();
    }
}
