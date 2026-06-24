using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Cartera de inversión de un usuario (Aggregate Root). Posee Holding (lotes de compra) y, a través de
/// ellos, Disposal (ventas). Mantiene RealizedPnL persistido (plusvalía/minusvalía realizada consolidada
/// en la moneda base del usuario). Las ventas son FIFO a nivel (Portfolio, Company). Toda mutación de
/// hijas/nietas pasa por esta clase: nunca se construyen ni mutan Holding/Disposal desde fuera.
/// </summary>
public class Portfolio : BaseEntity, IAggregateRoot
{
    public int IdPortfolio { get; private set; }
    public int IdUser { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money RealizedPnL { get; private set; } = null!;
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Holding> _holdings = [];
    public IReadOnlyCollection<Holding> Holdings => _holdings.AsReadOnly();

    protected Portfolio() { } // EF Core

    private Portfolio(int userId, string name, Money realizedPnL, EntityStatus idStatus, DateTime createdAt)
    {
        IdUser = userId;
        Name = name;
        RealizedPnL = realizedPnL;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Portfolio Create(int userId, string name, Currency baseCurrency)
    {
        if (userId <= 0)
            throw new ArgumentException("El identificador de usuario es obligatorio.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cartera es obligatorio.", nameof(name));

        return new Portfolio(userId, name.Trim(), Money.Create(0m, baseCurrency),
            EntityStatus.Active, DateTime.UtcNow);
    }

    /// <summary>Registra una compra como nuevo lote. La capa Application resuelve el rate (Company.Currency → base) a buyDate.</summary>
    public Holding AddHolding(int companyId, decimal shares, Money buyPrice, Currency baseCurrency,
        decimal rate, DateOnly buyDate, DateOnly rateDate, string? notes)
    {
        if (baseCurrency != RealizedPnL.Currency)
            throw new ArgumentException("La moneda base no coincide con la de la cartera.", nameof(baseCurrency));

        var avgBuyPrice = MoneyConversion.Create(buyPrice, baseCurrency, rate, rateDate);
        var holding = Holding.Create(companyId, shares, avgBuyPrice, buyDate, notes);
        _holdings.Add(holding);
        UpdatedAt = DateTime.UtcNow;
        return holding;
    }

    public void UpdateHolding(int holdingId, string? notes)
    {
        var holding = FindActiveHolding(holdingId);
        holding.UpdateNotes(notes);
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeleteHolding(int holdingId)
    {
        var holding = FindActiveHolding(holdingId);
        var reversed = holding.Delete();
        RealizedPnL = Money.Create(RealizedPnL.Amount - reversed, RealizedPnL.Currency);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Vende `shares` de una Company aplicando FIFO: consume los lotes abiertos más antiguos primero
    /// (orden BuyDate asc, desempate IdHolding asc), generando un Disposal por lote tocado. Acumula el
    /// realizado en RealizedPnL en el mismo SaveChanges. La capa Application resuelve el rate a sellDate.
    /// </summary>
    public IReadOnlyList<Disposal> SellShares(int companyId, decimal shares, Money sellPrice,
        Currency baseCurrency, decimal rate, DateOnly sellDate, DateOnly rateDate)
    {
        if (shares <= 0m)
            throw new ArgumentException("Las shares a vender deben ser mayores que cero.", nameof(shares));
        if (baseCurrency != RealizedPnL.Currency)
            throw new ArgumentException("La moneda base no coincide con la de la cartera.", nameof(baseCurrency));

        var openLots = _holdings
            .Where(h => h.IdCompany == companyId && h.IdStatus != EntityStatus.Deleted && h.OpenShares > 0m)
            .OrderBy(h => h.BuyDate).ThenBy(h => h.IdHolding)
            .ToList();

        var available = openLots.Sum(h => h.OpenShares);
        if (shares > available)
            throw new InsufficientSharesDomainException(companyId, shares, available);

        var disposals = new List<Disposal>();
        var remaining = shares;
        var realizedTotal = 0m;

        foreach (var lot in openLots)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(remaining, lot.OpenShares);
            // Money y MoneyConversion frescos por disposal: EF Core rastrea owned entities por referencia;
            // reutilizar la misma instancia de Money en múltiples disposals provoca que el batch INSERT
            // omita las columnas del segundo registro (bug de Pomelo con nested owned entities).
            var freshOriginal = Money.Create(sellPrice.Amount, sellPrice.Currency);
            var sellConversion = MoneyConversion.Create(freshOriginal, baseCurrency, rate, rateDate);
            var disposal = lot.RecordDisposal(take, sellConversion, sellDate, null);
            disposals.Add(disposal);
            realizedTotal += disposal.RealizedPnL.Amount;
            remaining -= take;
        }

        RealizedPnL = Money.Create(RealizedPnL.Amount + realizedTotal, RealizedPnL.Currency);
        UpdatedAt = DateTime.UtcNow;
        return disposals;
    }

    private Holding FindActiveHolding(int holdingId)
    {
        return _holdings.FirstOrDefault(h => h.IdHolding == holdingId && h.IdStatus != EntityStatus.Deleted)
            ?? throw new NotFoundException(nameof(Holding), holdingId);
    }
}
