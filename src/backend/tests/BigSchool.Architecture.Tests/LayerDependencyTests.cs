using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace BigSchool.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(BigSchool.Domain.SharedKernel.Entities.BaseEntity).Assembly;

    private static string Describe(TestResult r)
        => r.FailingTypeNames is null ? "-" : string.Join(", ", r.FailingTypeNames);

    [Fact]
    public void Domain_no_depende_de_Infrastructure_ni_EF()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOnAny("BigSchool.Infrastructure", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"Domain debe ser POCO. Fallos: {Describe(result)}");
    }

    [Fact]
    public void Domain_no_depende_de_Application()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot().HaveDependencyOn("BigSchool.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue($"Domain no debe conocer Application. Fallos: {Describe(result)}");
    }
}
