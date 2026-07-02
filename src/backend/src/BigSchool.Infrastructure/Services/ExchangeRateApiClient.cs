using System.Net.Http.Json;
using BigSchool.Application.SharedKernel.Configuration;
using BigSchool.Domain.SharedKernel.Enums;
using Dapper;
using Microsoft.Extensions.Options;
using BigSchool.Application.SharedKernel.Interfaces;
using BigSchool.Application.SharedKernel.Interfaces.Services;

namespace BigSchool.Infrastructure.Services;

/// <summary>
/// Anti-corruption layer de tipos de cambio. Cachea en ExchangeRates (Dapper, desacoplado del UoW
/// de negocio) y consulta Frankfurter (ECB) en caso de miss.
/// </summary>
public class ExchangeRateApiClient : IExchangeRateProvider
{
    private const string READCACHE_QUERY = @"SELECT Rate FROM ExchangeRates
                                             WHERE FromCurrency = @From AND ToCurrency = @To AND RateDate = @Date
                                             LIMIT 1;";

    private const string READLASTKNOWN_QUERY = @"SELECT Rate FROM ExchangeRates
                                                 WHERE FromCurrency = @From AND ToCurrency = @To
                                                 ORDER BY RateDate DESC LIMIT 1;";

    private const string UPSERTCACHE_QUERY = @"INSERT INTO ExchangeRates (FromCurrency, ToCurrency, Rate, RateDate, Source, FetchedAt)
                                               VALUES (@From, @To, @Rate, @Date, @Source, @Now)
                                               ON DUPLICATE KEY UPDATE Rate = @Rate, Source = @Source, FetchedAt = @Now;";

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
        var parameters = new DynamicParameters();
        parameters.Add("@From", from.ToString());
        parameters.Add("@To", to.ToString());
        parameters.Add("@Date", date.ToDateTime(TimeOnly.MinValue).Date);

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(READCACHE_QUERY, parameters);
    }

    private async Task<decimal?> ReadLastKnownAsync(Currency from, Currency to)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@From", from.ToString());
        parameters.Add("@To", to.ToString());

        using var conn = _dbFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<decimal?>(READLASTKNOWN_QUERY, parameters);
    }

    private async Task UpsertCacheAsync(Currency from, Currency to, decimal rate, DateOnly date, string source)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@From", from.ToString());
        parameters.Add("@To", to.ToString());
        parameters.Add("@Rate", rate);
        parameters.Add("@Date", date.ToDateTime(TimeOnly.MinValue).Date);
        parameters.Add("@Source", source);
        parameters.Add("@Now", DateTime.UtcNow);

        using var conn = _dbFactory.CreateConnection();
        await conn.ExecuteAsync(UPSERTCACHE_QUERY, parameters);
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
