using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class PutMeTests : AuthEndpointTestBase
{
    public PutMeTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PutMe_WithoutToken_Returns401()
        => (await Factory.CreateClient().PutAsJsonAsync("/api/v1/users/me", new { fullName = "X" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task PutMe_ValidFullName_UpdatesFullName_AndPersists()
    {
        var email = $"put-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterAsync(email, "Ada");
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "Ada Lovelace" });
        resp.EnsureSuccessStatusCode();
        var env = await resp.Content.ReadFromJsonAsync<ApiEnvelope<UserProfileResponse>>();
        env!.Data!.FullName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task PutMe_NewPassword_NewLoginSucceeds_AndOldLoginFails()
    {
        var email = $"pwd-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterAsync(email, "Ada"); // password DefaultPassword
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        (await client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "Ada", password = "NewSecret123!" }))
            .EnsureSuccessStatusCode();

        // Login con la NUEVA funciona (hashing Argon2 real, sin mocks).
        (await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = "NewSecret123!" })).StatusCode.Should().Be(HttpStatusCode.OK);
        // Login con la VIEJA falla.
        (await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { email, password = DefaultPassword })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record UserProfileResponse(int IdUser, string Email, string FullName, string BaseCurrency, string? LastLoginDate);
}
