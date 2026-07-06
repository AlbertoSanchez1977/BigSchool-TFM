namespace BigSchool.Application.Investments.DTOs;

public record HoldingPerformanceDto(
    int IdHolding, int IdCompany, string Ticker,
    decimal OpenShares, decimal CostBasis, decimal MarketValue,
    decimal UnrealizedPnL, decimal UnrealizedPnLPct,
    string BuyOriginalCurrency, decimal CostBasisOriginal,
    decimal MarketValueOriginal, decimal UnrealizedPnLOriginal);
