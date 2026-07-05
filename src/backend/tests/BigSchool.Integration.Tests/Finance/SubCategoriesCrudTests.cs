using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Finance;

[Collection(IntegrationCollection.Name)]
public class SubCategoriesCrudTests : TransactionEndpointTestBase
{
    public SubCategoriesCrudTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task PostSub_ValidSubCategory_CreatesAndAppearsInGetCategories()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var resp = await client.PostAsJsonAsync("/api/v1/categories/sub", new { mainCategory = "Luxuries", name = "Cine" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        (await CountAsync($"SELECT COUNT(*) FROM SubCategories WHERE IdUser={userId} AND Name='Cine'")).Should().Be(1);
        var cats = await (await client.GetAsync("/api/v1/categories")).Content.ReadAsStringAsync();
        cats.Should().Contain("Cine");
    }

    [Fact]
    public async Task PostSub_DuplicateOfGlobal_Returns409()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        // "Supermercado" es global predefinida (seed IdSubCategory=1, EssentialExpenses)
        var resp = await client.PostAsJsonAsync("/api/v1/categories/sub", new { mainCategory = "EssentialExpenses", name = "Supermercado" });
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteSub_OwnSubCategory_SoftDeletes()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var created = await (await client.PostAsJsonAsync("/api/v1/categories/sub", new { mainCategory = "Luxuries", name = "Cine" }))
            .Content.ReadFromJsonAsync<ApiEnvelope<SubCategoryResponse>>();
        var id = created!.Data!.IdSubCategory;

        (await client.DeleteAsync($"/api/v1/categories/sub/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await CountAsync($"SELECT COUNT(*) FROM SubCategories WHERE IdSubCategory={id} AND IdStatus=4")).Should().Be(1); // Deleted
    }

    [Fact]
    public async Task DeleteSub_GlobalSubCategory_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        (await client.DeleteAsync("/api/v1/categories/sub/1")).StatusCode.Should().Be(HttpStatusCode.NotFound); // IdSubCategory=1 global/default
    }

    private record SubCategoryResponse(int IdSubCategory, int IdMainCategory, string MainCategory, string Name);
}
