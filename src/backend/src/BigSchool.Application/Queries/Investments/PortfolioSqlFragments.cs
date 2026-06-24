namespace BigSchool.Application.Queries.Investments;

/// <summary>
/// Fragmentos SQL compartidos por las queries de cartera. HOLDING_VALUATION calcula, por cada lote,
/// las shares abiertas, el coste base, la última valoración convertida a la moneda base (con fallback
/// al último tipo ≤ fecha de la valoración) y el P/L no realizado. Parámetros requeridos por el
/// consumidor: @StatusDeleted (EntityStatus.Deleted) y @BaseCurrency (string ISO de la base del usuario).
/// </summary>
internal static class PortfolioSqlFragments
{
    public const string HOLDING_VALUATION = @"
SELECT
    x.IdHolding, x.IdPortfolio, x.IdCompany, x.Ticker, x.CompanyCurrency,
    x.Shares, x.OpenShares,
    x.BuyOriginalAmount, x.BuyOriginalCurrency, x.BuyExchangeRate,
    x.BuyBaseAmount, x.BuyBaseCurrency, x.BuyRateDate, x.BuyDate, x.Notes,
    x.LastPrice, x.LastDate, x.Rate,
    ROUND(x.OpenShares * COALESCE(x.LastPrice, 0) * x.Rate, 2) AS MarketValue,
    ROUND(x.OpenShares * x.BuyBaseAmount, 2) AS CostBasis,
    ROUND(x.OpenShares * COALESCE(x.LastPrice, 0) * x.Rate - x.OpenShares * x.BuyBaseAmount, 2) AS UnrealizedPnL
FROM (
    SELECT
        h.IdHolding, h.IdPortfolio, h.IdCompany, co.Ticker, co.Currency AS CompanyCurrency,
        h.Shares,
        (h.Shares - COALESCE((SELECT SUM(d.Shares) FROM Disposals d
                              WHERE d.IdHolding = h.IdHolding AND d.IdStatus <> @StatusDeleted), 0)) AS OpenShares,
        h.BuyOriginalAmount, h.BuyOriginalCurrency, h.BuyExchangeRate,
        h.BuyBaseAmount, h.BuyBaseCurrency, h.BuyRateDate, h.BuyDate, h.Notes,
        (SELECT v.Price FROM Valuations v WHERE v.IdCompany = h.IdCompany AND v.IdStatus <> @StatusDeleted
         ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1) AS LastPrice,
        (SELECT v.Date FROM Valuations v WHERE v.IdCompany = h.IdCompany AND v.IdStatus <> @StatusDeleted
         ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1) AS LastDate,
        CASE WHEN co.Currency = @BaseCurrency THEN 1
             ELSE COALESCE((SELECT er.Rate FROM ExchangeRates er
                            WHERE er.FromCurrency = co.Currency AND er.ToCurrency = @BaseCurrency
                              AND er.RateDate <= (SELECT v.Date FROM Valuations v
                                                  WHERE v.IdCompany = h.IdCompany AND v.IdStatus <> @StatusDeleted
                                                  ORDER BY v.Date DESC, v.IdValuation DESC LIMIT 1)
                            ORDER BY er.RateDate DESC LIMIT 1), 0)
        END AS Rate
    FROM Holdings h
    JOIN Companies co ON co.IdCompany = h.IdCompany
    WHERE h.IdStatus <> @StatusDeleted
) x";
}
