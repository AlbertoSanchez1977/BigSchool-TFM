using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetTransactionSummaryTests : TransactionEndpointTestBase
{
    public GetTransactionSummaryTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Summary_AfterIncomeAndExpense_ConsolidatesBalanceInBase()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Income, idMainCategory = (int)MainCategory.Salary, transactionDate = "2026-03-01", amount = 1500m });
        await CreateTransactionViaApiAsync(client, new { type = (int)TransactionType.Expense, idMainCategory = (int)MainCategory.EssentialExpenses, transactionDate = "2026-03-05", amount = 200m });

        var response = await client.GetAsync("/api/v1/transactions/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<SummaryPayload>>())!.Data!;
        dto.TotalIncome.Should().Be(1500m);
        dto.TotalExpense.Should().Be(200m);
        dto.Balance.Should().Be(1300m);
        dto.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Summary_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/summary");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
