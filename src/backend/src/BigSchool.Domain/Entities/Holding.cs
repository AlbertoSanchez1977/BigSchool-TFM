using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Lote de compra (tranche) de una Company dentro de un Portfolio. Entidad hija del agregado.
/// Shares es inmutable (las compradas); OpenShares se deriva restando las disposiciones activas.
/// AvgBuyPrice es un snapshot MoneyConversion congelado a BuyDate. Alta vía Portfolio.AddHolding.
/// No se construye ni se muta nunca fuera del AR (Portfolio).
/// </summary>
public class Holding : BaseEntity
{
    public int IdHolding { get; private set; }
    public int IdCompany { get; private set; }
    public decimal Shares { get; private set; }
    public MoneyConversion AvgBuyPrice { get; private set; } = null!;
    public DateOnly BuyDate { get; private set; }
    public string? Notes { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Disposal> _disposals = [];
    public IReadOnlyCollection<Disposal> Disposals => _disposals.AsReadOnly();

    /// <summary>Shares aún abiertas = compradas − vendidas (disposiciones activas).</summary>
    public decimal OpenShares =>
        Shares - _disposals.Where(d => d.IdStatus != EntityStatus.Deleted).Sum(d => d.Shares);

    /// <summary>Un lote está cerrado cuando ya no quedan shares abiertas.</summary>
    public bool IsClosed => OpenShares == 0m;

    protected Holding() { } // EF Core

    private Holding(int companyId, decimal shares, MoneyConversion avgBuyPrice, DateOnly buyDate,
        string? notes, EntityStatus idStatus, DateTime createdAt)
    {
        IdCompany = companyId;
        Shares = shares;
        AvgBuyPrice = avgBuyPrice;
        BuyDate = buyDate;
        Notes = notes;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    internal static Holding Create(int companyId, decimal shares, MoneyConversion avgBuyPrice,
        DateOnly buyDate, string? notes)
    {
        if (companyId <= 0)
            throw new ArgumentException("El identificador de empresa es obligatorio.", nameof(companyId));
        if (shares <= 0m)
            throw new ArgumentException("Las shares deben ser mayores que cero.", nameof(shares));

        return new Holding(companyId, shares, avgBuyPrice, buyDate,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            EntityStatus.Active, DateTime.UtcNow);
    }

    /// <summary>Registra una venta de este lote. El SellPrice (MoneyConversion) lo construye el AR (Portfolio). RealizedPnL = (SellPrice.Base − AvgBuyPrice.Base) × shares, en moneda base.</summary>
    internal Disposal RecordDisposal(decimal shares, MoneyConversion sellPrice, DateOnly sellDate, string? notes)
    {
        if (shares <= 0m)
            throw new ArgumentException("Las shares vendidas deben ser mayores que cero.", nameof(shares));
        if (shares > OpenShares)
            throw new ArgumentException("No se pueden vender más shares de las abiertas en el lote.", nameof(shares));

        var baseCurrency = AvgBuyPrice.Base.Currency;
        var realizedAmount = (sellPrice.Base.Amount - AvgBuyPrice.Base.Amount) * shares;
        var realizedPnL = Money.Create(realizedAmount, baseCurrency);

        var disposal = Disposal.Create(shares, sellPrice, realizedPnL, sellDate, notes);
        _disposals.Add(disposal);
        UpdatedAt = DateTime.UtcNow;
        return disposal;
    }

    internal void UpdateNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-delete del lote y de sus disposiciones activas. Devuelve la suma del RealizedPnL revertido
    /// para que el Portfolio ajuste su columna RealizedPnL y mantenga el invariante.
    /// </summary>
    internal decimal Delete()
    {
        var reversed = 0m;
        foreach (var disposal in _disposals.Where(d => d.IdStatus != EntityStatus.Deleted))
        {
            reversed += disposal.RealizedPnL.Amount;
            disposal.Delete();
        }
        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
        return reversed;
    }
}
