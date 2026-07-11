using BigSchool.Domain.Auth.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using BigSchool.Infrastructure.SharedKernel.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BigSchool.Infrastructure.Auth.Persistence.Configurations;

namespace BigSchool.Infrastructure.Auth.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.IdUser);

        ConfigureProperties(builder);
        ConfigureIndexes(builder);
        ConfigureFilters(builder);

        builder.Ignore(u => u.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.IdUser).ValueGeneratedOnAdd();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.PasswordSalt).IsRequired().HasMaxLength(256);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.BaseCurrency)
            .IsRequired()
            .HasConversion(CurrencyConverter.CharIso)
            .HasColumnType("char(3)")
            .HasDefaultValueSql("'EUR'");
        builder.Property(u => u.LastLoginDate);
        builder.Property(u => u.IdStatus)
            .IsRequired()
            .HasDefaultValue(EntityStatus.Active)
            .HasConversion<short>();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
    }

    private static void ConfigureFilters(EntityTypeBuilder<User> builder)
    {
        builder.HasQueryFilter(u => u.IdStatus != EntityStatus.Deleted);
    }
}
