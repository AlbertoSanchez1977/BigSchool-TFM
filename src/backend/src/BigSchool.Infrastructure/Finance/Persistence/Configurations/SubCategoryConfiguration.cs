using BigSchool.Domain.Finance.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Finance.Persistence.Configurations;

public class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        builder.ToTable("SubCategories");
        builder.HasKey(s => s.IdSubCategory);

        ConfigureProperties(builder);
        ConfigureFilters(builder);

        builder.Ignore(s => s.DomainEvents);
    }

    private static void ConfigureProperties(EntityTypeBuilder<SubCategory> builder)
    {
        builder.Property(s => s.IdSubCategory).ValueGeneratedOnAdd();
        builder.Property(s => s.IdMainCategory).IsRequired().HasConversion<int>();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.IsDefault).HasDefaultValue(false);
        builder.Property(s => s.IdStatus)
            .IsRequired()
            .HasDefaultValue(EntityStatus.Active)
            .HasConversion<short>();
        builder.Property(s => s.CreatedAt).IsRequired();
        // IdUser explícito (nullable; NULL = global). Referencia suave por Id, SIN FK dura.
        builder.Property(s => s.IdUser);
    }

    private static void ConfigureFilters(EntityTypeBuilder<SubCategory> builder)
    {
        builder.HasQueryFilter(s => s.IdStatus != EntityStatus.Deleted);
    }
}
