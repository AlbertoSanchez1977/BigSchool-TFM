using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Auth;

[Collection(IntegrationCollection.Name)]
public class GetMeTests : AuthEndpointTestBase
{
    public GetMeTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/users/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task GetMe_Authenticated_ReturnsProfileFromToken()
    {
        var email = $"me-{Guid.NewGuid():N}@test.com";
        var auth = await RegisterAsync(email, "Ada Lovelace"); // EUR por defecto
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var env = await (await client.GetAsync("/api/v1/users/me"))
            .Content.ReadFromJsonAsync<ApiEnvelope<UserProfileResponse>>();
        env!.Data!.Email.Should().Be(email);
        env.Data!.FullName.Should().Be("Ada Lovelace");
        env.Data!.BaseCurrency.Should().Be("EUR");
    }

    private record UserProfileResponse(int IdUser, string Email, string FullName, string BaseCurrency, string? LastLoginDate);
}
