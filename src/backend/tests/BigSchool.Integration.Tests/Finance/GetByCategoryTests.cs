using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Finance;

[Collection(IntegrationCollection.Name)]
public class GetByCategoryTests : TransactionEndpointTestBase
{
    public GetByCategoryTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ByCategory_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/transactions/by-category"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task ByCategory_RangeMoreThan4Years_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var resp = await client.GetAsync("/api/v1/transactions/by-category?from=2020-01-01&to=2026-01-01");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ByCategory_AggregatesByCategory_OverBaseAmount()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-01", amount = 10m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-02", amount = 20m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-03-03", amount = 5m });

        var env = await (await client.GetAsync("/api/v1/transactions/by-category?type=Expense"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<CategoryTotalResponse>>>();
        env!.Data!.Single(c => c.IdMainCategory == (int)MainCategory.EssentialExpenses).Total.Should().Be(30m);
    }

    [Fact]
    public async Task ByCategory_FilterByFromTo_ExcludesOutOfRangeTransactions()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-01-01", amount = 100m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-06-01", amount = 7m });

        var env = await (await client.GetAsync("/api/v1/transactions/by-category?from=2026-05-01&to=2026-07-01"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<CategoryTotalResponse>>>();
        env!.Data!.Single(c => c.IdMainCategory == (int)MainCategory.Luxuries).Total.Should().Be(7m);
    }

    private record CategoryTotalResponse(int IdMainCategory, string MainCategory, decimal Total);
}
