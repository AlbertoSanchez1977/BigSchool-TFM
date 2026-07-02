using BigSchool.Infrastructure.SharedKernel.IntegrationEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BigSchool.Infrastructure.SharedKernel.Persistence;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(x => x.IdOutboxMessage);
        builder.Property(x => x.EventId).IsRequired();
        builder.HasIndex(x => x.EventId).IsUnique();
        builder.Property(x => x.Type).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Payload).IsRequired().HasColumnType("json");
        builder.Property(x => x.OccurredOn).IsRequired();
        builder.Property(x => x.ProcessedOn);
        builder.Property(x => x.Error).HasMaxLength(2048);
        builder.HasIndex(x => x.ProcessedOn); // acelera el drenado de pendientes
    }
}
