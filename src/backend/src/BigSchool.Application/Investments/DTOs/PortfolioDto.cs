using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.Investments.DTOs;

/// <summary>DTO de respuesta de comando (construido desde el dominio): Currency tipado.</summary>
public record PortfolioDto(int IdPortfolio, string Name, decimal RealizedPnL, Currency RealizedPnLCurrency);
