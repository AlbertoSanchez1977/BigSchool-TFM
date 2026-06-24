using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
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
}
