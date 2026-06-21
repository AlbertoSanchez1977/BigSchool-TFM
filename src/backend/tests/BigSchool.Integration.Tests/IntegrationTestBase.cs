using BigSchool.Integration.Tests.Fixtures;
using Xunit;

namespace BigSchool.Integration.Tests;

/// <summary>
/// Ciclo de vida común a todos los tests E2E: reset de la BD de test antes de cada test
/// y un BigSchoolWebAppFactory propio (pipeline real) por test. Las clases derivadas deben
/// llevar [Collection(IntegrationCollection.Name)].
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly MySqlDatabaseFixture Fixture;
    protected BigSchoolWebAppFactory Factory = null!;

    protected IntegrationTestBase(MySqlDatabaseFixture fixture) => Fixture = fixture;

    public async Task InitializeAsync()
    {
        await Fixture.ResetAsync();
        Factory = new BigSchoolWebAppFactory(Fixture.ConnectionString);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }

    // ---- Tipos de deserialización del envelope ApiResponse<T> (System.Text.Json, camelCase) ----
    protected record ApiEnvelope<T>(T? Data, List<ApiErrorPayload> Errors, MetaPayload? Meta);
    protected record ApiErrorPayload(string Code, string Message, string? Field);
    protected record MetaPayload(int? Page, int? PageSize, int? TotalCount, int? TotalPages);
}
