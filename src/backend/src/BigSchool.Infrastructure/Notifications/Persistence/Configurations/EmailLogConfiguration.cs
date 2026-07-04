using BigSchool.Domain.Notifications.Entities;
using BigSchool.Domain.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.Notifications.Persistence.Configurations;

public class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> b)
    {
        b.ToTable("EmailLogs");
        b.HasKey(e => e.IdEmailLog);
        b.Property(e => e.IdEmailLog).ValueGeneratedOnAdd();
        b.Property(e => e.IdUser); // NULL permitido; referencia suave por Id, SIN FK dura (frontera de módulo)
        b.Property(e => e.Recipient).IsRequired().HasMaxLength(255);
        b.Property(e => e.Subject).IsRequired().HasMaxLength(300);
        b.Property(e => e.Body).IsRequired().HasMaxLength(4000);
        b.Property(e => e.Type).IsRequired().HasConversion<short>();
        b.Property(e => e.SentAt).IsRequired();
        b.Property(e => e.IdStatus).IsRequired().HasDefaultValue(EntityStatus.Active).HasConversion<short>();
        b.Property(e => e.CreatedAt).IsRequired();
        b.HasIndex(e => e.IdUser);
        b.HasQueryFilter(e => e.IdStatus != EntityStatus.Deleted);
        b.Ignore(e => e.DomainEvents);
    }
}
