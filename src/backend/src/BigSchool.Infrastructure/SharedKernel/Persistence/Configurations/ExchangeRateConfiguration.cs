using BigSchool.Domain.SharedKernel.Entities;
using BigSchool.Infrastructure.SharedKernel.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BigSchool.Infrastructure.SharedKernel.Persistence.Configurations;

namespace BigSchool.Infrastructure.SharedKernel.Persistence.Configurations;

public class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("ExchangeRates");
        b.HasKey(e => e.IdExchangeRate);

        b.Property(e => e.IdExchangeRate).ValueGeneratedOnAdd();
        b.Property(e => e.FromCurrency).HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        b.Property(e => e.ToCurrency).HasConversion(CurrencyConverter.CharIso).HasColumnType("char(3)");
        b.Property(e => e.Rate).HasColumnType("decimal(18,6)");
        b.Property(e => e.RateDate).HasColumnType("date");
        b.Property(e => e.Source).HasMaxLength(100);
        b.Property(e => e.FetchedAt).IsRequired();

        b.HasIndex(e => new { e.FromCurrency, e.ToCurrency, e.RateDate }).IsUnique();
    }
}
