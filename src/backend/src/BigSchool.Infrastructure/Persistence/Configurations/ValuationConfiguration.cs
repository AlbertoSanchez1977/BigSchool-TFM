using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;
public class ValuationConfiguration : IEntityTypeConfiguration<Valuation>
{
    public void Configure(EntityTypeBuilder<Valuation> builder)
    {
        builder.ToTable("Valuations");
        builder.HasKey(v => v.IdValuation);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);
        
        builder.Ignore(v => v.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Valuation> builder)
    {
        builder.Property(v => v.IdValuation).ValueGeneratedOnAdd();
        builder.Property(v => v.Date).HasColumnType("date").IsRequired();
        builder.Property(v => v.Source).HasMaxLength(100);
        builder.Property(v => v.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(v => v.CreatedAt).IsRequired();
        builder.Property(v => v.UpdatedAt);

        // Shadow FK IdCompany (la relación la declara CompanyConfiguration).
        builder.Property<int>("IdCompany");

    }

    private static void ConfigureRelationships(EntityTypeBuilder<Valuation> builder)
    {
        builder.OwnsOne(v => v.Price, p =>
        {
            p.Property(m => m.Amount).HasColumnName("Price").HasColumnType("decimal(18,4)");
            p.Property(m => m.Currency).HasColumnName("PriceCurrency")
                .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        });
        builder.Navigation(v => v.Price).IsRequired();

    }

    private static void ConfigureIndexes(EntityTypeBuilder<Valuation> builder)
    {
        builder.HasIndex("IdCompany", nameof(Valuation.Date)).IsUnique();
    }

    private static void ConfigureFilters(EntityTypeBuilder<Valuation> builder)
    {
        builder.HasQueryFilter(v => v.IdStatus != EntityStatus.Deleted);
    }
}
