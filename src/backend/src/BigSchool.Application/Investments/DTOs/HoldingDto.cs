using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.Investments.DTOs;

/// <summary>DTO de respuesta de comando para un lote (Holding): monedas tipadas, snapshot AvgBuyPrice.</summary>
public record HoldingDto(
    int IdHolding,
    int IdCompany,
    decimal Shares,
    decimal BuyOriginalAmount,
    Currency BuyOriginalCurrency,
    decimal BuyExchangeRate,
    decimal BuyBaseAmount,
    Currency BuyBaseCurrency,
    DateOnly BuyRateDate,
    DateOnly BuyDate,
    string? Notes);
