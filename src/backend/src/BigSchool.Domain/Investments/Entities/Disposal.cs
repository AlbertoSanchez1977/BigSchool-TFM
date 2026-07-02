using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.ValueObjects;

namespace BigSchool.Domain.Investments.Entities;

/// <summary>
/// Venta (disposición) de shares de un lote (Holding). Entidad nieta del agregado Portfolio.
/// SellPrice es un snapshot MoneyConversion congelado a SellDate; RealizedPnL se calcula en el dominio.
/// Alta vía Holding.RecordDisposal (interno al agregado). No se construye nunca fuera del AR.
/// </summary>
public class Disposal : BaseEntity
{
    public int IdDisposal { get; private set; }
    public decimal Shares { get; private set; }
    public MoneyConversion SellPrice { get; private set; } = null!;
    public DateOnly SellDate { get; private set; }
    public Money RealizedPnL { get; private set; } = null!;
    public string? Notes { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Disposal() { } // EF Core

    private Disposal(decimal shares, MoneyConversion sellPrice, Money realizedPnL,
        DateOnly sellDate, string? notes, EntityStatus idStatus, DateTime createdAt)
    {
        Shares = shares;
        SellPrice = sellPrice;
        RealizedPnL = realizedPnL;
        SellDate = sellDate;
        Notes = notes;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    internal static Disposal Create(decimal shares, MoneyConversion sellPrice, Money realizedPnL,
        DateOnly sellDate, string? notes)
    {
        return new Disposal(shares, sellPrice, realizedPnL, sellDate,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            EntityStatus.Active, DateTime.UtcNow);
    }

    internal void Delete()
    {
        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }
}
