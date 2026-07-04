using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Notifications;

[Collection(IntegrationCollection.Name)]
public class GetContactsTests : NotificationEndpointTestBase
{
    public GetContactsTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/contacts")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Get_Authenticated_ReturnsGlobalInboxPaginated()
    {
        var (userId, email) = await SeedUserAsync();
        var pub = Factory.CreateClient();
        await pub.PostAsJsonAsync("/api/v1/contacts", new { fullName = "A", email = "a@x.com", message = "m" });
        await pub.PostAsJsonAsync("/api/v1/contacts", new { fullName = "B", email = "b@x.com", message = "m" });

        var env = await (await AuthenticatedClient(userId, email).GetAsync("/api/v1/contacts?page=1&pageSize=1"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<ContactListItemResponse>>>();
        env!.Data!.Should().HaveCount(1);
        env.Meta!.TotalCount.Should().Be(2);
    }
}
