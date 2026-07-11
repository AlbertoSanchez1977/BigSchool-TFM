using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Finance.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Finance;

[Collection(IntegrationCollection.Name)]
public class PutTransactionTests : TransactionEndpointTestBase
{
    public PutTransactionTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Put_ExistingTransaction_UpdatesFields_AndPersists()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var id = await CreateTransactionViaApiAsync(client, new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            description = "Original",
            transactionDate = "2026-03-10",
            amount = 50m
        });

        var response = await client.PutAsJsonAsync($"/api/v1/transactions/{id}", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.Luxuries,
            description = "Modificado",
            transactionDate = "2026-03-12",
            amount = 75.25m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>())!.Data!;
        dto.IdTransaction.Should().Be(id);
        dto.IdMainCategory.Should().Be("Luxuries");
        dto.Description.Should().Be("Modificado");
        dto.OriginalAmount.Should().Be(75.25m);
        dto.TransactionDate.Should().Be("2026-03-12");
        (await CountActiveTransactionsAsync(userId)).Should().Be(1);  // update, no duplica
    }

    [Fact]
    public async Task Put_NonExisting_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PutAsJsonAsync("/api/v1/transactions/999999", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            transactionDate = "2026-03-12",
            amount = 10m
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Put_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/v1/transactions/1", new
        {
            type = 1, idMainCategory = 1, transactionDate = "2026-03-12", amount = 10m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
