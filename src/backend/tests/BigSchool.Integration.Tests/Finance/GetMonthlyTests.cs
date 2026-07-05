using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Finance;

[Collection(IntegrationCollection.Name)]
public class GetMonthlyTests : TransactionEndpointTestBase
{
    public GetMonthlyTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Monthly_WithoutToken_Returns401()
        => (await Factory.CreateClient().GetAsync("/api/v1/transactions/monthly"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Monthly_RangeMoreThan4Years_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var resp = await client.GetAsync("/api/v1/transactions/monthly?from=2020-01-01&to=2026-01-01");
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Monthly_GroupsByYearMonth_WithIncomeAndExpenseSplit()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-01", amount = 10m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-03-15", amount = 1000m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-04-01", amount = 5m });

        var env = await (await client.GetAsync("/api/v1/transactions/monthly?from=2026-01-01&to=2026-12-31"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<MonthlyChartPayload>>>();

        var march = env!.Data!.Single(p => p.Year == 2026 && p.Month == 3);
        march.Income.Should().Be(1000m);
        march.Expense.Should().Be(10m);
        var april = env.Data!.Single(p => p.Year == 2026 && p.Month == 4);
        april.Income.Should().Be(0m);
        april.Expense.Should().Be(5m);
    }

    [Fact]
    public async Task Monthly_FilterByCategoryAndType_ReturnsOnlyMatching()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-05-01", amount = 10m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-05-02", amount = 25m });

        var env = await (await client.GetAsync($"/api/v1/transactions/monthly?category={(int)MainCategory.Luxuries}&type=Expense"))
            .Content.ReadFromJsonAsync<ApiEnvelope<List<MonthlyChartPayload>>>();

        var may = env!.Data!.Single(p => p.Year == 2026 && p.Month == 5);
        may.Expense.Should().Be(25m);
    }

    [Fact]
    public async Task MonthlyChart_ByYear_StillWorks()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-02-01", amount = 500m });

        var resp = await client.GetAsync("/api/v1/transactions/monthly-chart?year=2026");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
