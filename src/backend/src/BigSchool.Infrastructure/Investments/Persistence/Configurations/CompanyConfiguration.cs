using BigSchool.Domain.Investments.Entities;
using BigSchool.Domain.Investments.Enums;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BigSchool.Infrastructure.Investments.Persistence.Configurations;

namespace BigSchool.Infrastructure.Investments.Persistence.Configurations;
public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.IdCompany);

        ConfigureProperties(builder);
        ConfigureRelationships(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(c => c.DomainEvents);
    }
    private void ConfigureProperties(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.IdCompany).ValueGeneratedOnAdd();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Ticker).IsRequired().HasMaxLength(10);
        builder.Property(c => c.Sector).HasConversion<string>().HasMaxLength(100);
        builder.Property(c => c.Market).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Currency).IsRequired()
           .HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)").HasDefaultValueSql("'EUR'");
        builder.Property(c => c.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
    }

    private void ConfigureRelationships(EntityTypeBuilder<Company> builder)
    {
        builder.HasMany(c => c.Valuations)
            .WithOne()
            .HasForeignKey("IdCompany")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Valuations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
    private void ConfigureIndexes(EntityTypeBuilder<Company> builder)
    {
        builder.HasIndex(c => c.Ticker).IsUnique();
    }


    private void ConfigureFilters(EntityTypeBuilder<Company> builder)
    {
        builder.HasQueryFilter(c => c.IdStatus != EntityStatus.Deleted);
    }
}
