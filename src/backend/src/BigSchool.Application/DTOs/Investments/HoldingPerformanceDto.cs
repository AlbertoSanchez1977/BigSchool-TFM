namespace BigSchool.Application.DTOs.Investments;

public record HoldingPerformanceDto(
    int IdHolding, int IdCompany, string Ticker,
    decimal OpenShares, decimal CostBasis, decimal MarketValue,
    decimal UnrealizedPnL, decimal UnrealizedPnLPct);
