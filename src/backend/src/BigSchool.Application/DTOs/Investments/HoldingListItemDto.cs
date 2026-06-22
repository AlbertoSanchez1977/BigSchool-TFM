namespace BigSchool.Application.DTOs.Investments;

public record HoldingListItemDto(
    int IdHolding, int IdCompany, string Ticker, string CompanyCurrency,
    decimal Shares, decimal OpenShares,
    decimal BuyOriginalAmount, string BuyOriginalCurrency, decimal BuyExchangeRate,
    decimal BuyBaseAmount, string BuyBaseCurrency, DateOnly BuyRateDate, DateOnly BuyDate, string? Notes,
    decimal MarketValue, decimal CostBasis, decimal UnrealizedPnL);
