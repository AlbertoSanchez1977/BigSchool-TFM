using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder.ToTable("Holdings");
        builder.HasKey(h => h.IdHolding);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(h => h.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Holding> builder)
    {
        builder.Property(h => h.IdHolding).ValueGeneratedOnAdd();
        builder.Property(h => h.IdCompany).IsRequired();
        builder.Property(h => h.Shares).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(h => h.BuyDate).HasColumnType("date").IsRequired();
        builder.Property(h => h.Notes).HasMaxLength(500);
        builder.Property(h => h.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(h => h.CreatedAt).IsRequired();
        builder.Property(h => h.UpdatedAt);

        // Shadow FK al AR (la relación la declara PortfolioConfiguration).
        builder.Property<int>("IdPortfolio");

        builder.OwnsOne(h => h.AvgBuyPrice, conv =>
        {
            conv.Property(c => c.Rate).HasColumnName("BuyExchangeRate").HasColumnType("decimal(18,6)");
            conv.Property(c => c.RateDate).HasColumnName("BuyRateDate").HasColumnType("date");

            conv.OwnsOne(c => c.Original, orig =>
            {
                orig.Property(m => m.Amount).HasColumnName("BuyOriginalAmount").HasColumnType("decimal(18,4)");
                orig.Property(m => m.Currency).HasColumnName("BuyOriginalCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
            conv.OwnsOne(c => c.Base, baseMoney =>
            {
                baseMoney.Property(m => m.Amount).HasColumnName("BuyBaseAmount").HasColumnType("decimal(18,2)");
                baseMoney.Property(m => m.Currency).HasColumnName("BuyBaseCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
        });
        builder.Navigation(h => h.AvgBuyPrice).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Holding> builder)
    {
        builder.HasOne<Company>().WithMany().HasForeignKey(h => h.IdCompany)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(h => h.Disposals)
            .WithOne()
            .HasForeignKey("IdHolding")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(h => h.Disposals).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Holding> builder)
    {
        builder.HasIndex("IdPortfolio", nameof(Holding.IdCompany));
    }

    private static void ConfigureFilters(EntityTypeBuilder<Holding> builder)
    {
        builder.HasQueryFilter(h => h.IdStatus != EntityStatus.Deleted);
    }
}
