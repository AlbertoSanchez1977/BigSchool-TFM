using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BigSchool.Integration.Tests.Fixtures;
using Dapper;
using FluentAssertions;
using MySqlConnector;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class RegisterTests : AuthEndpointTestBase
{
    public RegisterTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    // ── EXHAUSTIVO: response completa + persistencia + hash NO en claro + round-trip con login ──
    [Fact]
    public async Task Register_NewUser_ReturnsToken_PersistsHashedPassword_AndCanLogin()
    {
        var email = $"reg-{Guid.NewGuid():N}@test.com";

        var response = await RegisterRawAsync(email, DefaultPassword, "Ana Pérez");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        dto.Email.Should().Be(email);
        dto.FullName.Should().Be("Ana Pérez");

        // El password se persiste HASHEADO (Argon2), nunca en claro.
        var storedHash = await ReadPasswordHashAsync(email);
        storedHash.Should().NotBeNullOrWhiteSpace();
        storedHash.Should().NotBe(DefaultPassword);

        // Round-trip REAL hash+verify: el mismo password permite hacer login (lo que un unitario mockeado no prueba).
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = DefaultPassword });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_EmailAlreadyExists()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var response = await RegisterRawAsync(email, DefaultPassword, "Otro Nombre");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "EMAIL_ALREADY_EXISTS");
    }

    [Theory]
    [InlineData("not-an-email", "Secret123!", "Nombre")]   // email inválido
    [InlineData("ok@test.com", "short", "Nombre")]          // password < 8
    [InlineData("ok@test.com", "Secret123!", "")]           // fullName vacío
    public async Task Register_InvalidPayload_Returns400_ValidationError(string email, string password, string fullName)
    {
        var response = await RegisterRawAsync(email, password, fullName);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Register_NewUser_CreatesSingleWelcomeEmailLogAndDrainsOutbox()
    {
        var email = $"welcome-{Guid.NewGuid():N}@test.com";
        var resp = await RegisterRawAsync(email, DefaultPassword, "Ada Lovelace");
        resp.EnsureSuccessStatusCode();

        await using var conn = new MySqlConnection(Fixture.ConnectionString);
        var idUser = await conn.ExecuteScalarAsync<int>("SELECT IdUser FROM Users WHERE Email=@email", new { email });

        // Frontera Auth↔Notifications: lo único que Auth conoce de primera mano es que su propio
        // outbox se drenó. EXACTAMENTE 1 fila procesada: si PublishIntegrationEventHandler se
        // registrara también en Autofac (además de en MediatR/el bus), se dispararía 2 veces → 2 filas.
        (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE ProcessedOn IS NOT NULL"))
            .Should().Be(1);
        (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OutboxMessages WHERE ProcessedOn IS NULL"))
            .Should().Be(0);

        var payload = await conn.ExecuteScalarAsync<string>(
            "SELECT Payload FROM OutboxMessages WHERE Type LIKE '%UserRegisteredIntegrationEvent%' ORDER BY IdOutboxMessage DESC LIMIT 1");
        using var json = JsonDocument.Parse(payload);
        json.RootElement.GetProperty("IdUser").GetInt32().Should().Be(idUser);
        json.RootElement.GetProperty("Email").GetString().Should().Be(email);

        // Cruce de frontera DELIBERADO (solo válido en monolito modular con BD compartida): confirma
        // que Notifications efectivamente creó el EmailLog de bienvenida. En un microservicio estricto
        // Auth no podría consultar EmailLogs (tabla de otro servicio) y este assert se eliminaría.
        (await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM EmailLogs WHERE Recipient=@email AND Type=1 AND IdUser=@idUser", new { email, idUser }))
            .Should().Be(1);
    }
}
