using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.Investments.DTOs;

public record DisposalDto(
    int IdDisposal,
    int IdHolding,
    decimal Shares,
    decimal SellOriginalAmount,
    Currency SellOriginalCurrency,
    decimal SellExchangeRate,
    decimal SellBaseAmount,
    Currency SellBaseCurrency,
    DateOnly SellRateDate,
    DateOnly SellDate,
    decimal RealizedPnL,
    Currency RealizedPnLCurrency,
    string? Notes);
