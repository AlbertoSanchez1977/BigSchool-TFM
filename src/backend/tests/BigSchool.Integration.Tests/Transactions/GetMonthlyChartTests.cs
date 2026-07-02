using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetMonthlyChartTests : TransactionEndpointTestBase
{
    public GetMonthlyChartTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task MonthlyChart_GroupsByMonth_WithIncomeAndExpense()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-03-01", amount = 1500m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-10", amount = 300m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.Luxuries, transactionDate = "2026-04-05", amount = 120m });

        var response = await client.GetAsync("/api/v1/transactions/monthly-chart?year=2026");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var points = (await response.Content.ReadFromJsonAsync<ApiEnvelope<List<MonthlyChartPayload>>>())!.Data!;
        var march = points.Single(p => p.Month == 3);
        march.Year.Should().Be(2026);
        march.Income.Should().Be(1500m);
        march.Expense.Should().Be(300m);
        var april = points.Single(p => p.Month == 4);
        april.Income.Should().Be(0m);
        april.Expense.Should().Be(120m);
    }

    [Fact]
    public async Task MonthlyChart_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/monthly-chart?year=2026");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
