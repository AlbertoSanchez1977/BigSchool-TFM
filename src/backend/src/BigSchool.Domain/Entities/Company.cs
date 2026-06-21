using BigSchool.Domain.Enums;
using BigSchool.Domain.Exceptions;
using BigSchool.Domain.ValueObjects;

namespace BigSchool.Domain.Entities;

/// <summary>
/// Empresa cotizada (Aggregate Root global, sin IdUser). Posee Valuation como entidad hija.
/// </summary>
public class Company : BaseEntity, IAggregateRoot
{
    public int IdCompany { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Ticker { get; private set; } = string.Empty;
    public string? Sector { get; private set; }
    public string? Market { get; private set; }
    public Currency Currency { get; private set; }
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Valuation> _valuations = [];
    public IReadOnlyCollection<Valuation> Valuations => _valuations.AsReadOnly();

    protected Company() { } // EF Core

    private Company(string name, string ticker, string? sector, string? market,
        Currency currency, EntityStatus idStatus, DateTime createdAt)
    {
        Name = name;
        Ticker = ticker;
        Sector = sector;
        Market = market;
        Currency = currency;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Company Create(string name, string ticker, string? sector, string? market, Currency currency)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(ticker))
            throw new ArgumentException("El ticker es obligatorio.", nameof(ticker));

        return new Company(
            name.Trim(),
            ticker.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(sector) ? null : sector.Trim(),
            string.IsNullOrWhiteSpace(market) ? null : market.Trim(),
            currency,
            EntityStatus.Active,
            DateTime.UtcNow);
    }

    public Valuation AddValuation(decimal price, DateOnly date, string? source)
    {
        if (price <= 0m)
            throw new ArgumentException("El precio debe ser mayor que cero.", nameof(price));

        if (_valuations.Any(v => v.Date == date && v.IdStatus != EntityStatus.Deleted))
            throw new DuplicateValuationDomainException(Ticker, date);

        var valuation = Valuation.Create(Money.Create(price, Currency), date, source?.Trim());
        _valuations.Add(valuation);
        return valuation;
    }
}
