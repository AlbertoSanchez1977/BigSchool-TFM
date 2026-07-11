using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using MySqlConnector;

namespace BigSchool.Integration.Tests.Auth;

/// <summary>Helpers específicos de los endpoints de autenticación.</summary>
public abstract class AuthEndpointTestBase : IntegrationTestBase
{
    protected AuthEndpointTestBase(MySqlDatabaseFixture fixture) : base(fixture) { }

    protected const string DefaultPassword = "Secret123!";

    // Registra un usuario vía el endpoint REAL y devuelve la respuesta HTTP (sin assertions).
    // baseCurrency default EUR: no rompe los tests existentes que no la especifican.
    protected Task<HttpResponseMessage> RegisterRawAsync(string email, string password, string fullName, string baseCurrency = "EUR")
        => Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email, password, fullName, baseCurrency });

    // Registra y devuelve el AuthResponse deserializado (asume éxito).
    protected async Task<AuthResponse> RegisterAsync(string email, string fullName = "E2E User")
    {
        var resp = await RegisterRawAsync(email, DefaultPassword, fullName);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>())!.Data!;
    }

    // Lee el PasswordHash crudo de un usuario (para verificar que NO se persiste en claro).
    protected async Task<string?> ReadPasswordHashAsync(string email)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        return await conn.QuerySingleOrDefaultAsync<string?>(
            "SELECT PasswordHash FROM Users WHERE Email = @email;", new { email });
    }

    // Elimina físicamente un usuario (para el caso refresh con usuario inexistente).
    protected async Task DeleteUserAsync(string email)
    {
        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        await conn.ExecuteAsync("DELETE FROM Users WHERE Email = @email;", new { email });
    }

    protected record AuthResponse(string AccessToken, DateTime ExpiresAt, string Email, string FullName);
}
