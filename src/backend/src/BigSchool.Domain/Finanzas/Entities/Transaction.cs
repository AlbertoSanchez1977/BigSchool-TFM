using BigSchool.Domain.Finanzas.Enums;
using BigSchool.Domain.Finanzas.Events;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Domain.SharedKernel.ValueObjects;

namespace BigSchool.Domain.Finanzas.Entities;

public class Transaction : BaseEntity, IAggregateRoot
{
    public int IdTransaction { get; private set; }
    public int IdUser { get; private set; }
    public TransactionType Type { get; private set; }
    public MainCategory IdMainCategory { get; private set; }
    public int? IdSubCategory { get; private set; }
    public string? Description { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public MoneyConversion Conversion { get; private set; } = null!;
    public EntityStatus IdStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected Transaction() { } // EF Core

    private Transaction(
        int userId,
        TransactionType type,
        MainCategory category,
        int? subCategoryId,
        string? description,
        DateOnly transactionDate,
        MoneyConversion conversion,
        EntityStatus idStatus,
        DateTime createdAt)
    {
        IdUser = userId;
        Type = type;
        IdMainCategory = category;
        IdSubCategory = subCategoryId;
        Description = description;
        TransactionDate = transactionDate;
        Conversion = conversion;
        IdStatus = idStatus;
        CreatedAt = createdAt;
    }

    public static Transaction Create(
        int userId,
        TransactionType type,
        MainCategory category,
        int? subCategoryId,
        string? description,
        Money original,
        Currency baseCurrency,
        decimal rate,
        DateOnly transactionDate,
        DateOnly rateDate)
    {
        if (userId <= 0)
            throw new ArgumentException("El identificador de usuario es obligatorio.", nameof(userId));
        if (original.Amount <= 0m)
            throw new ArgumentException("El importe debe ser mayor que cero.", nameof(original));

        var transaction = new Transaction(
            userId,
            type,
            category,
            subCategoryId,
            description?.Trim(),
            transactionDate,
            MoneyConversion.Create(original, baseCurrency, rate, rateDate),
            EntityStatus.Active,
            DateTime.UtcNow);

        transaction.RaiseDomainEvent(new TransactionCreatedEvent(transaction));

        return transaction;
    }

    public void Update(
        TransactionType type,
        MainCategory category,
        int? subCategoryId,
        string? description,
        Money original,
        Currency baseCurrency,
        decimal rate,
        DateOnly transactionDate,
        DateOnly rateDate)
    {
        if (original.Amount <= 0m)
            throw new ArgumentException("El importe debe ser mayor que cero.", nameof(original));

        Type = type;
        IdMainCategory = category;
        IdSubCategory = subCategoryId;
        Description = description?.Trim();
        TransactionDate = transactionDate;
        Conversion = MoneyConversion.Create(original, baseCurrency, rate, rateDate);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IdStatus = EntityStatus.Deleted;
        UpdatedAt = DateTime.UtcNow;
    }
}
