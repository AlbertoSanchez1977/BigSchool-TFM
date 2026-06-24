using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetTransactionsTests : TransactionEndpointTestBase
{
    public GetTransactionsTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    private async Task SeedSampleSetAsync(HttpClient client)
    {
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-01", amount = 10m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-15", amount = 20m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-03-20", amount = 30m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-04-01", amount = 1500m });
    }

    [Fact]
    public async Task Get_NoFilters_ReturnsAll_WithPaginationMeta()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync("/api/v1/transactions?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(4);
        env.Meta!.Page.Should().Be(1);
        env.Meta.PageSize.Should().Be(20);
        env.Meta.TotalCount.Should().Be(4);
        env.Data![0].TransactionDate.Should().Be("2026-04-01");  // orden fecha desc
    }

    [Fact]
    public async Task Get_FilterByTypeExpense_ReturnsOnlyExpenses()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync($"/api/v1/transactions?type={(int)TransactionType.Expense}");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(3);
        env.Data!.Should().OnlyContain(t => t.Type == (short)TransactionType.Expense);
    }

    [Fact]
    public async Task Get_FilterByDateRange_ReturnsOnlyInRange()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync("/api/v1/transactions?from=2026-03-01&to=2026-03-31");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(3);
        env.Data!.Should().OnlyContain(t => t.TransactionDate.StartsWith("2026-03"));
    }

    [Fact]
    public async Task Get_SecondPage_RespectsPageSize()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await SeedSampleSetAsync(client);

        var response = await client.GetAsync("/api/v1/transactions?page=2&pageSize=2");

        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TransactionListItemResponse>>>();
        env!.Data!.Should().HaveCount(2);
        env.Meta!.TotalCount.Should().Be(4);
        env.Meta.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
