using System.Net;
using System.Net.Http.Json;
using BigSchool.Domain.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Transactions;

[Collection(IntegrationCollection.Name)]
public class PostTransactionTests : TransactionEndpointTestBase
{
    public PostTransactionTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_ExpenseEur_ReturnsFullDto_AndPersistsAllFields()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            idSubCategory = 1,
            description = "Compra semanal",
            transactionDate = "2026-03-15",
            amount = 185.50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>();
        env!.Errors.Should().BeEmpty();
        var dto = env.Data!;
        dto.IdTransaction.Should().BeGreaterThan(0);
        dto.Type.Should().Be("Expense");
        dto.IdMainCategory.Should().Be("EssentialExpenses");
        dto.IdSubCategory.Should().Be(1);
        dto.Description.Should().Be("Compra semanal");
        dto.TransactionDate.Should().Be("2026-03-15");
        dto.OriginalAmount.Should().Be(185.50m);
        dto.OriginalCurrency.Should().Be("EUR");
        dto.ExchangeRate.Should().Be(1m);
        dto.BaseAmount.Should().Be(185.50m);
        dto.BaseCurrency.Should().Be("EUR");
        dto.RateDate.Should().Be("2026-03-15");

        await using var conn = new MySqlConnector.MySqlConnection(Fixture.ConnectionString);
        var row = await Dapper.SqlMapper.QuerySingleAsync(conn,
            @"SELECT Type, IdMainCategory, OriginalCurrency, BaseAmount, IdStatus
              FROM Transactions WHERE IdTransaction = @id;", new { id = dto.IdTransaction });
        ((short)row.Type).Should().Be((short)TransactionType.Expense);
        ((int)row.IdMainCategory).Should().Be((int)MainCategory.EssentialExpenses);
        ((string)row.OriginalCurrency).Should().Be("EUR");
        ((decimal)row.BaseAmount).Should().Be(185.50m);
        ((short)row.IdStatus).Should().Be((short)EntityStatus.Active);
    }

    [Fact]
    public async Task Post_IncomeUsd_UsesCachedRate_AndConsolidatesInBase()
    {
        var (userId, email) = await SeedUserAsync(Currency.EUR);
        var client = AuthenticatedClient(userId, email);
        await SeedExchangeRateAsync(Currency.USD, Currency.EUR, 0.90m, new DateOnly(2026, 4, 10));

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Income,
            idMainCategory = (int)MainCategory.Other,
            description = "Pago USD",
            transactionDate = "2026-04-10",
            amount = 100.00m,
            currency = "USD"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<ApiEnvelope<TransactionResponse>>())!.Data!;
        dto.OriginalAmount.Should().Be(100.00m);
        dto.OriginalCurrency.Should().Be("USD");
        dto.ExchangeRate.Should().Be(0.90m);
        dto.BaseAmount.Should().Be(90.00m);
        dto.BaseCurrency.Should().Be("EUR");
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = 1, idMainCategory = 1, transactionDate = "2026-03-15", amount = 10m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AmountZero_Returns400_WithValidationError()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = (int)MainCategory.EssentialExpenses,
            transactionDate = "2026-03-15",
            amount = 0m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR" && e.Field == "Amount");
    }

    [Fact]
    public async Task Post_InvalidMainCategory_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);

        var response = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            type = (int)TransactionType.Expense,
            idMainCategory = 999,
            transactionDate = "2026-03-15",
            amount = 50m
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var env = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        env!.Errors.Should().Contain(e => e.Code == "VALIDATION_ERROR");
    }
}
