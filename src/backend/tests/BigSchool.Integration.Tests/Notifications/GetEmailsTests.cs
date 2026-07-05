using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Notifications;

[Collection(IntegrationCollection.Name)]
public class GetEmailsTests : NotificationEndpointTestBase
{
    public GetEmailsTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/emails")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Get_SeesOwnWelcomeAndGlobalContactAck_ButNotOtherUsersWelcome()
    {
        var emailA = $"a-{Guid.NewGuid():N}@test.com";
        var pub = Factory.CreateClient();
        await pub.PostAsJsonAsync("/api/v1/auth/register", new { email = emailA, password = "Passw0rd!", fullName = "A" });
        await pub.PostAsJsonAsync("/api/v1/contacts", new { fullName = "Vis", email = "vis@x.com", message = "m" });
        var emailB = $"b-{Guid.NewGuid():N}@test.com";
        await pub.PostAsJsonAsync("/api/v1/auth/register", new { email = emailB, password = "Passw0rd!", fullName = "B" });

        var idA = await ScalarAsync<int>($"SELECT IdUser FROM Users WHERE Email='{emailA}'");
        var env = await (await AuthenticatedClient(idA, emailA).GetAsync("/api/v1/emails?page=1&pageSize=50"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<EmailLogListItemResponse>>>();

        env!.Data!.Should().Contain(e => e.Recipient == emailA && e.Type == 1);
        env.Data!.Should().Contain(e => e.Type == 2 && e.IdUser == null);
        env.Data!.Should().NotContain(e => e.Recipient == emailB);
    }

    [Fact]
    public async Task GetById_OtherUsersWelcome_Returns404()
    {
        var emailB = $"b-{Guid.NewGuid():N}@test.com";
        await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email = emailB, password = "Passw0rd!", fullName = "B" });
        var idWelcomeB = await ScalarAsync<int>($"SELECT IdEmailLog FROM EmailLogs WHERE Recipient='{emailB}' AND Type=1");

        var (idA, emailA) = await SeedUserAsync();
        var resp = await AuthenticatedClient(idA, emailA).GetAsync($"/api/v1/emails/{idWelcomeB}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
