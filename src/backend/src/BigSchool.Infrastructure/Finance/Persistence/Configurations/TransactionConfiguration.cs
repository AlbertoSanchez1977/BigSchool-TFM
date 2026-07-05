using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BigSchool.Infrastructure.Finance.Persistence.Configurations;

namespace BigSchool.Infrastructure.Finance.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("Transactions");
        b.HasKey(t => t.IdTransaction);

        ConfigureProperties(b);
        ConfigureConversion(b);
        ConfigureRelationships(b);
        ConfigureIndexes(b);

        b.HasQueryFilter(t => t.IdStatus != EntityStatus.Deleted);
        b.Ignore(t => t.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Transaction> b)
    {
        b.Property(t => t.IdTransaction).ValueGeneratedOnAdd();
        b.Property(t => t.IdUser).IsRequired();
        b.Property(t => t.Type).IsRequired().HasConversion<short>();
        b.Property(t => t.IdMainCategory).IsRequired().HasConversion<int>();
        b.Property(t => t.IdSubCategory);
        b.Property(t => t.Description).HasMaxLength(255);
        b.Property(t => t.TransactionDate).HasColumnType("date").IsRequired();
        b.Property(t => t.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        b.Property(t => t.CreatedAt).IsRequired();
        b.Property(t => t.UpdatedAt);
    }

    private static void ConfigureConversion(EntityTypeBuilder<Transaction> b)
    {
        b.OwnsOne(t => t.Conversion, conv =>
        {
            conv.Property(c => c.Rate).HasColumnName("ExchangeRate").HasColumnType("decimal(18,6)");
            conv.Property(c => c.RateDate).HasColumnName("RateDate").HasColumnType("date");

            conv.OwnsOne(c => c.Original, orig =>
            {
                orig.Property(m => m.Amount).HasColumnName("OriginalAmount").HasColumnType("decimal(18,2)");
                orig.Property(m => m.Currency).HasColumnName("OriginalCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });

            conv.OwnsOne(c => c.Base, baseMoney =>
            {
                baseMoney.Property(m => m.Amount).HasColumnName("BaseAmount").HasColumnType("decimal(18,2)");
                baseMoney.Property(m => m.Currency).HasColumnName("BaseCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
        });

        b.Navigation(t => t.Conversion).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Transaction> b)
    {
        b.HasOne<User>().WithMany().HasForeignKey(t => t.IdUser)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<SubCategory>().WithMany().HasForeignKey(t => t.IdSubCategory)
            .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Transaction> b)
    {
        b.HasIndex(t => new { t.IdUser, t.TransactionDate });
    }
}
