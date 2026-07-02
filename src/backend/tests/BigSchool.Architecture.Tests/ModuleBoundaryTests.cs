using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace BigSchool.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly Assembly Domain = typeof(BigSchool.Domain.SharedKernel.Entities.BaseEntity).Assembly;
    private static readonly Assembly Application = typeof(BigSchool.Application.SharedKernel.Common.ApiResponse).Assembly;

    // Auth -> Finanzas: excepción deliberada y documentada (spec 005 §7). SubCategory sigue siendo
    // entidad hija del agregado User hasta que la Spec 009 la re-modele como AR independiente de
    // Finanzas; hasta entonces, User referencia SubCategory/MainCategory/DuplicateSubCategoryDomainException.
    // No se relaja aquí sin más: el plan 018 prohíbe explícitamente re-modelar SubCategory en esta tarea.
    [Theory]
    [InlineData("BigSchool.Domain.Auth", new[] { "BigSchool.Domain.Investments" })]
    [InlineData("BigSchool.Domain.Finanzas", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Investments" })]
    [InlineData("BigSchool.Domain.Investments", new[] { "BigSchool.Domain.Auth", "BigSchool.Domain.Finanzas" })]
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
    [InlineData("BigSchool.Application.Auth", new[] { "BigSchool.Application.Finanzas", "BigSchool.Application.Investments" })]
    [InlineData("BigSchool.Application.Finanzas", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Investments" })]
    [InlineData("BigSchool.Application.Investments", new[] { "BigSchool.Application.Auth", "BigSchool.Application.Finanzas" })]
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
