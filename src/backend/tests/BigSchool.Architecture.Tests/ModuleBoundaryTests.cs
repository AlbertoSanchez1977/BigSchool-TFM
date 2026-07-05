using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace BigSchool.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly Assembly Domain = typeof(BigSchool.Domain.SharedKernel.Entities.BaseEntity).Assembly;
    private static readonly Assembly Application = typeof(BigSchool.Application.SharedKernel.Common.ApiResponse).Assembly;

    [Theory]
    [InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Finanzas", "BigSchool.Domain.Investments", "BigSchool.Domain.Notifications" })]
    [InlineData("BigSchool.Domain.Finanzas", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Investments", "BigSchool.Domain.Notifications" })]
    [InlineData("BigSchool.Domain.Investments", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Finanzas", "BigSchool.Domain.Notifications" })]
    [InlineData("BigSchool.Domain.Notifications", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Finanzas", "BigSchool.Domain.Investments" })]
    public void Modulos_de_dominio_no_dependen_entre_si(string moduleNs, string[] forbidden)
    {
        var result = Types.InAssembly(Domain)
            .That().ResideInNamespace(moduleNs)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{moduleNs} no debe depender de otros módulos de negocio. Fallos: {Describe(result)}");
    }

    [Theory]
    [InlineData("BigSchool.Application.Auth", new[] { "BigSchool.Application.Finanzas", "BigSchool.Application.Investments", "BigSchool.Application.Notifications" })]
    [InlineData("BigSchool.Application.Finanzas", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Investments", "BigSchool.Application.Notifications" })]
    [InlineData("BigSchool.Application.Investments", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Finanzas", "BigSchool.Application.Notifications" })]
    [InlineData("BigSchool.Application.Notifications", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Finanzas", "BigSchool.Application.Investments" })]
    public void Modulos_de_application_no_dependen_entre_si(string moduleNs, string[] forbidden)
    {
        var result = Types.InAssembly(Application)
            .That().ResideInNamespace(moduleNs)
            .ShouldNot().HaveDependencyOnAny(forbidden)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"{moduleNs} no debe depender de otros módulos de negocio. Fallos: {Describe(result)}");
    }

    private static string Describe(TestResult result)
        => result.FailingTypeNames is null ? "-" : string.Join(", ", result.FailingTypeNames);
}
