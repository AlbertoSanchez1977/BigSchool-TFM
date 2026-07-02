using BigSchool.Domain.SharedKernel.Enums;

namespace BigSchool.Application.DTOs.Investments;

/// <summary>DTO de respuesta de comando (construido desde el dominio): Currency tipado.</summary>
public record ValuationDto(int IdValuation, int IdCompany, decimal Price, Currency Currency, DateOnly Date, string? Source);
