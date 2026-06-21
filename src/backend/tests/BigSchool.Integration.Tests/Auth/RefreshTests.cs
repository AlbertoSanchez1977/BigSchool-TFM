using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class RefreshTests : AuthEndpointTestBase
{
    public RefreshTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    private HttpClient ClientWithBearer(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsUsableToken()
    {
        var email = $"refresh-{Guid.NewGuid():N}@test.com";
        var registered = await RegisterAsync(email);

        var client = ClientWithBearer(registered.AccessToken);
        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthResponse>>())!.Data!;
        dto.AccessToken.Should().NotBeNullOrWhiteSpace();
        dto.Email.Should().Be(email);

        // El token refrescado también es válido contra un endpoint protegido.
        var client2 = ClientWithBearer(dto.AccessToken);
        var protected_ = await client2.GetAsync("/api/v1/transactions/summary");
        protected_.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithoutToken_Returns401()
    {
        var response = await Factory.CreateClient().PostAsync("/api/v1/auth/refresh", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Token de firma válida pero cuyo usuario ya no existe → el handler lanza Unauthorized (401).
    [Fact]
    public async Task Refresh_UserDeletedAfterTokenIssued_Returns401()
    {
        var email = $"deleted-{Guid.NewGuid():N}@test.com";
        var registered = await RegisterAsync(email);
        await DeleteUserAsync(email);

        var client = ClientWithBearer(registered.AccessToken);
        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
