using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class LoginTests : AuthEndpointTestBase
{
    public LoginTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    // El token emitido por login debe ser válido contra un endpoint protegido (JWT real, extremo a extremo).
    [Fact]
    public async Task Login_ValidCredentials_ReturnsUsableToken()
    {
        var email = $"login-{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>())!.Data!;
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.Email.Should().Be(email);

        // Usar el token en un endpoint [Authorize] → 200 (no 401): valida emisión + validación JwtBearer reales.
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", dto.AccessToken);
        var protected_ = await client.GetAsync("/api/v1/transactions/summary");
        protected_.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns400_InvalidCredentials()
    {
        var email = $"wrongpw-{Guid.NewGuid():N}@test.com";
        await RegisterAsync(email);

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "WrongPassword9!" });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await login.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "INVALID_CREDENTIALS");
    }

    // Usuario inexistente devuelve el MISMO error que password incorrecta (no filtra existencia → seguridad).
    [Fact]
    public async Task Login_NonExistentUser_Returns400_InvalidCredentials()
    {
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = $"ghost-{Guid.NewGuid():N}@test.com", password = DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await login.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_EmptyEmail_Returns400_ValidationError()
    {
        var login = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email = "", password = DefaultPassword });

        login.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await login.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }
}
