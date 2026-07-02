using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Cotización puntual de una Company (entidad hija). Price es un Money en la moneda de la empresa;
/// no lleva conversión a base de usuario (el catálogo es global). Alta vía Company.AddValuation.
/// </summary>
public class Valuation : BaseEntity
{
    public int IdValuation { get; private set; }
    public Money Price { get; private set; } = null!;
    public DateOnly Date { get; private set; }
    public string? Source { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Valuation() { } // EF Core

    private Valuation(Money price, DateOnly date, string? source, EntityStatus idStatus, DateTime createdAt)
    {
        Price = price;
        Date = date;
        Source = source;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    internal static Valuation Create(Money price, DateOnly date, string? source)
    {
        return new Valuation(price, date, source, EntityStatus.Active, DateTime.UtcNow);
    }
}
