using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class GetTransactionByIdTests : TransactionEndpointTestBase
{
    public GetTransactionByIdTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_ExistingOwnTransaction_ReturnsListItemDto()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        var id = await CreateTransactionViaApiAsync(client, new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            idSubCategory = 1,
            description = "Compra",
            transactionDate = "2026-03-20",
            amount = 42.30m
        });

        var response = await client.GetAsync($"/api/v1/transactions/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionListItemResponse>>())!.Data!;
        dto.IdTransaction.Should().Be(id);
        dto.Type.Should().Be((short)TransactionType.Expense);
        dto.IdMainCategory.Should().Be((int)MainCategory.EssentialExpenses);
        dto.OriginalAmount.Should().Be(42.30m);
        dto.OriginalCurrency.Should().Be("EUR");
        dto.TransactionDate.Should().Be("2026-03-20");
    }

    [Fact]
    public async Task GetById_NonExisting_Returns404()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.GetAsync("/api/v1/transactions/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/api/v1/transactions/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
