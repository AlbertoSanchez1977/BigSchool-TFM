using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>Resultado de una venta FIFO: los Disposal generados (uno por lote tocado) y el realizado consolidado de la cartera.</summary>
public record SellSharesResultDto(
    IReadOnlyList<DisposalDto> Disposals,
    decimal PortfolioRealizedPnL,
    Currency RealizedPnLCurrency);
