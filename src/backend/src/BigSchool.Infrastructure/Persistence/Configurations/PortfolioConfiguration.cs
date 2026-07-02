using BigSchool.Domain.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

public class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(p => p.IdPortfolio);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(p => p.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<Portfolio> builder)
    {
        builder.Property(p => p.IdPortfolio).ValueGeneratedOnAdd();
        builder.Property(p => p.IdUser).IsRequired();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.OwnsOne(p => p.RealizedPnL, m =>
        {
            m.Property(x => x.Amount).HasColumnName("RealizedPnL").HasColumnType("decimal(18,2)");
            m.Property(x => x.Currency).HasColumnName("RealizedPnLCurrency")
                .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        });
        builder.Navigation(p => p.RealizedPnL).IsRequired();
    }

    private static void ConfigureRelationships(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.IdUser)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Holdings)
            .WithOne()
            .HasForeignKey("IdPortfolio")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Holdings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasIndex(p => p.IdUser);
    }

    private static void ConfigureFilters(EntityTypeBuilder<Portfolio> builder)
    {
        builder.HasQueryFilter(p => p.IdStatus != EntityStatus.Deleted);
    }
}
