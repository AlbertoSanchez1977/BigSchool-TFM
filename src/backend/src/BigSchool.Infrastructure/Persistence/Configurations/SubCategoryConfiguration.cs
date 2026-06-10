using BigSchool.Domain.Entities;
using BigSchool.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Persistence.Configurations;

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
        // Shadow property IdUser (FK gestionada en UserConfiguration)
        builder.Property<int?>("IdUser");
    }

    private static void ConfigureFilters(EntityTypeBuilder<SubCategory> builder)
    {
        builder.HasQueryFilter(s => s.IdStatus != EntityStatus.Deleted);
    }
}
