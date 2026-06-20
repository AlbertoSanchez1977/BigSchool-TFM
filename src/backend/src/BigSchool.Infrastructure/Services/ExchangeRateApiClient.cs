using System.Net.Http.Json;
using BigSchool.Application.Configuration;
using BigSchool.Application.Interfaces;
using BigSchool.Application.Interfaces.Services;
using BigSchool.Domain.Enums;
using Dapper;
using Microsoft.Extensions.Options;

namespace BigSchool.Infrastructure.Services;

/// <summary>
/// Anti-corruption layer de tipos de cambio. Cachea en ExchangeRates (Dapper, desacoplado del UoW
/// de negocio) y consulta Frankfurter (ECB) en caso de miss.
/// </summary>
public class ExchangeRateApiClient : IExchangeRateProvider
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ExchangeRateSettings _settings;

    public ExchangeRateApiClient(
        IDbConnectionFactory dbFactory,
        IHttpClientFactory httpFactory,
        IOptions<AppSettings> settings)
    {
        _dbFactory = dbFactory;
        _httpFactory = httpFactory;
        _settings = settings.Value.ExchangeRate;
    }

    public async Task<decimal> GetRateAsync(Currency from, Currency to, DateOnly date, CancellationToken ct = default)
    {
        if (from == to)
            return 1m;

        var cached = await ReadCacheAsync(from, to, date);
        if (cached is not null)
            return cached.Value;

        decimal rate;
        try
        {
            rate = await FetchFromProviderAsync(from, to, date, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var last = await ReadLastKnownAsync(from, to);
            if (last is not null)
                return last.Value;
            throw new InvalidOperationException(
                $"No se pudo obtener el tipo de cambio {from}->{to} para {date:yyyy-MM-dd} y no hay valor cacheado.", ex);
        }

        await UpsertCacheAsync(from, to, rate, date, "frankfurter");
        return rate;
    }

    private async Task<decimal?> ReadCacheAsync(Currency from, Currency to, DateOnly date)
    {
        const string sql = """
            SELECT Rate FROM ExchangeRates
            WHERE FromCurrency = @From AND ToCurrency = @To AND RateDate = @Date
            LIMIT 1;
            """;
        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(sql,
            new { From = from.ToString(), To = to.ToString(), Date = date.ToDateTime(TimeOnly.MinValue).Date });
    }

    private async Task<decimal?> ReadLastKnownAsync(Currency from, Currency to)
    {
        const string sql = """
            SELECT Rate FROM ExchangeRates
            WHERE FromCurrency = @From AND ToCurrency = @To
            ORDER BY RateDate DESC LIMIT 1;
            """;
        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(sql,
            new { From = from.ToString(), To = to.ToString() });
    }

    private async Task UpsertCacheAsync(Currency from, Currency to, decimal rate, DateOnly date, string source)
    {
        const string sql = """
            INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
            VALUES (@From, @To, @Rate, @Date, @Source, @Now)
            ON DUPLICATE KEY UPDATE Rate = @Rate, Source = @Source, FetchedAt = @Now;
            """;
        using var conn = _dbFactory.CreateConnection();
        await conn.ExecuteAsync(sql, new
        {
            From = from.ToString(),
            To = to.ToString(),
            Rate = rate,
            Date = date.ToDateTime(TimeOnly.MinValue).Date,
            Source = source,
            Now = DateTime.UtcNow
        });
    }

    private async Task<decimal> FetchFromProviderAsync(Currency from, Currency to, DateOnly date, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient();
        var url = $"{_settings.BaseUrl.TrimEnd('/')}/{date:yyyy-MM-dd}?from={from}&to={to}";
        var response = await http.GetFromJsonAsync<FrankfurterResponse>(url, ct)
            ?? throw new HttpRequestException($"Respuesta vacía del proveedor de tipos para {url}.");

        if (!response.Rates.TryGetValue(to.ToString(), out var rate))
            throw new HttpRequestException($"El proveedor no devolvió tipo para {to} en {url}.");

        return rate;
    }
}
