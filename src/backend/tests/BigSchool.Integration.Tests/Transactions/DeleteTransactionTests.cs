using System.Net;
using BigSchool.Domain.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class DeleteTransactionTests : TransactionEndpointTestBase
{
    public DeleteTransactionTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_ExistingTransaction_SoftDeletes_AndHidesFromReads()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var id = await CreateTransactionViaApiAsync(client, new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            transactionDate = "2026-03-10",
            amount = 50m
        });

        var response = await client.DeleteAsync($"/api/v1/transactions/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadStatusAsync(id)).Should().Be((short)EntityStatus.Deleted);  // soft-delete: la fila persiste
        (await CountActiveTransactionsAsync(userId)).Should().Be(0);
        var getAfter = await client.GetAsync($"/api/v1/transactions/{id}");
        getAfter.StatusCode.Should().Be(HttpStatusCode.NotFound);               // query filter la oculta
    }

    [Fact]
    public async Task Delete_NonExisting_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.DeleteAsync("/api/v1/transactions/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.DeleteAsync("/api/v1/transactions/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
