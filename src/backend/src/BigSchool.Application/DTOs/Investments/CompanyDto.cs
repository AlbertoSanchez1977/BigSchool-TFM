using BigSchool.Domain.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de respuesta de comando (construido desde el dominio): Currency tipado.</summary>
public record CompanyDto(int IdCompany, string Name, string Ticker, Sector? Sector, Market? Market, Currency Currency);
