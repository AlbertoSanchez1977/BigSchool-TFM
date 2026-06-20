using Xunit;

namespace BigSchool.Integration.Tests.Fixtures;

/// <summary>
/// Agrupa TODAS las clases de integración en una colección que comparte un único MySqlDatabaseFixture.
/// Al estar en la misma colección, xUnit las ejecuta en serie (no en paralelo): seguro para una BD compartida.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    public const string Name = "Integration";
}
