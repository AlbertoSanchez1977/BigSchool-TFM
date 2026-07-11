using System.Net;
using System.Net.Http.Json;
using BigSchool.Integration.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace BigSchool.Integration.Tests.Investments;

[Collection(IntegrationCollection.Name)]
public class GetValuationSeriesTests : CompanyEndpointTestBase
{
    public GetValuationSeriesTests(MySqlDatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Series_InvalidPeriod_Returns400()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        (await client.GetAsync("/api/v1/companies/1/valuations/series?period=99")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Series_OneYear_ReturnsPointsInWindow_OrderedWithCoherentSummary()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"S{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new { name = "Serie Co.", ticker, currency = "USD" });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 100m, date = "2025-06-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 150m, date = "2026-03-10", source = (string?)null });

        var s = (await (await client.GetAsync($"/api/v1/companies/{id}/valuations/series?period=OneYear"))
            .Content.ReadFromJsonAsync<ApiEnvelope<ValuationSeriesResponse>>())!.Data!;
        s.Currency.Should().Be("USD");
        s.Points.Should().HaveCount(2);
        s.Points[0].Date.Should().Be("2025-06-10"); // ventana: [2026-03-10 - 12m, 2026-03-10]
        s.Summary.First.Should().Be(100m);
        s.Summary.Last.Should().Be(150m);
        s.Summary.ChangePct.Should().Be(50m);
    }

    [Fact]
    public async Task Series_CompanyWithoutValuations_ReturnsEmptySeries()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"E{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new { name = "Empty Co.", ticker, currency = "USD" });

        var s = (await (await client.GetAsync($"/api/v1/companies/{id}/valuations/series?period=OneYear"))
            .Content.ReadFromJsonAsync<ApiEnvelope<ValuationSeriesResponse>>())!.Data!;
        s.Points.Should().BeEmpty();
        s.Summary.First.Should().Be(0m);
        s.Summary.Last.Should().Be(0m);
    }

    [Fact]
    public async Task Series_ThreeMonths_ExcludesPointsOutsideWindow()
    {
        var (userId, email) = await SeedUserAsync();
        var client = AuthenticatedClient(userId, email);
        var ticker = $"W{Guid.NewGuid():N}".Substring(0, 8).ToUpperInvariant();
        var id = await CreateCompanyViaApiAsync(client, new { name = "Window Co.", ticker, currency = "USD" });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 80m, date = "2025-01-10", source = (string?)null });
        await client.PostAsJsonAsync($"/api/v1/companies/{id}/valuations", new { price = 200m, date = "2026-03-10", source = (string?)null });

        var s = (await (await client.GetAsync($"/api/v1/companies/{id}/valuations/series?period=ThreeMonths"))
            .Content.ReadFromJsonAsync<ApiEnvelope<ValuationSeriesResponse>>())!.Data!;
        s.Points.Should().ContainSingle();
        s.Points[0].Date.Should().Be("2026-03-10");
    }

    private record ValuationPointResponse(string Date, decimal Price);
    private record ValuationSeriesSummaryResponse(decimal First, decimal Last, decimal Min, decimal Max, decimal ChangePct);
    private record ValuationSeriesResponse(string Currency, List<ValuationPointResponse> Points, ValuationSeriesSummaryResponse Summary);
}
