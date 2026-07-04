using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Notifications.Persistence.Configurations;

public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> b)
    {
        b.ToTable("Contacts");
        b.HasKey(c => c.IdContact);
        b.Property(c => c.IdContact).ValueGeneratedOnAdd();
        b.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        b.Property(c => c.Email).IsRequired().HasMaxLength(255);
        b.Property(c => c.Message).IsRequired().HasMaxLength(2000);
        b.Property(c => c.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        b.Property(c => c.CreatedAt).IsRequired();
        b.HasQueryFilter(c => c.IdStatus != EntityStatus.Deleted);
        b.Ignore(c => c.DomainEvents);
    }
}
