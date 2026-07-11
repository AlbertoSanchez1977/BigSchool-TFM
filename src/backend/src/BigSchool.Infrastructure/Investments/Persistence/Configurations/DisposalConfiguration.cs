using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BigSchool.Infrastructure.Investments.Persistence.Configurations;

namespace BigSchool.Infrastructure.Investments.Persistence.Configurations;

public class DisposalConfiguration : IEntityTypeConfiguration<Disposal>
{
    public void Configure(EntityTypeBuilder<Disposal> builder)
    {
        builder.ToTable("Disposals");
        builder.HasKey(d => d.IdDisposal);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(d => d.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Disposal> builder)
    {
        builder.Property(d => d.IdDisposal).ValueGeneratedOnAdd();
        builder.Property(d => d.Shares).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(d => d.SellDate).HasColumnType("date").IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(500);
        builder.Property(d => d.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt);

        // Shadow FK al lote padre (la relación la declara HoldingConfiguration).
        builder.Property<int>("IdHolding");

        builder.OwnsOne(d => d.SellPrice, conv =>
        {
            conv.Property(c => c.Rate).HasColumnName("SellExchangeRate").HasColumnType("decimal(18,6)");
            conv.Property(c => c.RateDate).HasColumnName("SellRateDate").HasColumnType("date");

            conv.OwnsOne(c => c.Original, orig =>
            {
                orig.Property(m => m.Amount).HasColumnName("SellOriginalAmount").HasColumnType("decimal(18,4)");
                orig.Property(m => m.Currency).HasColumnName("SellOriginalCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
            conv.OwnsOne(c => c.Base, baseMoney =>
            {
                baseMoney.Property(m => m.Amount).HasColumnName("SellBaseAmount").HasColumnType("decimal(18,2)");
                baseMoney.Property(m => m.Currency).HasColumnName("SellBaseCurrency")
                    .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
            });
        });
        builder.Navigation(d => d.SellPrice).IsRequired();

        builder.OwnsOne(d => d.RealizedPnL, m =>
        {
            m.Property(x => x.Amount).HasColumnName("RealizedPnL").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("RealizedPnLCurrency")
                .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        });
        builder.Navigation(d => d.RealizedPnL).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Disposal> builder)
    {
        // La relación Holding→Disposal la declara HoldingConfiguration (HasMany/WithOne/HasForeignKey "IdHolding").
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Disposal> builder)
    {
        builder.HasIndex("IdHolding");
    }

    private static void ConfigureFilters(EntityTypeBuilder<Disposal> builder)
    {
        builder.HasQueryFilter(d => d.IdStatus != EntityStatus.Deleted);
    }
}
