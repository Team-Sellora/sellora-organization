using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration
  : IEntityTypeConfiguration<OutboxMessage>
{
  public void Configure(EntityTypeBuilder<OutboxMessage> builder)
  {
    builder.ToTable("outbox_message");

    builder.HasKey(message => message.OutboxId)
      .HasName("pk_outbox_message");

    builder.Property(message => message.OutboxId)
      .HasColumnName("outbox_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(message => message.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(message => message.AggregateType)
      .HasColumnName("aggregate_type")
      .HasMaxLength(80)
      .IsRequired();

    builder.Property(message => message.AggregateId)
      .HasColumnName("aggregate_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(message => message.EventType)
      .HasColumnName("event_type")
      .HasMaxLength(120)
      .IsRequired();

    builder.Property(message => message.Payload)
      .HasColumnName("payload")
      .HasColumnType("jsonb")
      .IsRequired();

    builder.Property(message => message.CorrelationId)
      .HasColumnName("correlation_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(message => message.OccurredAt)
      .HasColumnName("occurred_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.Property(message => message.PublishedAt)
      .HasColumnName("published_at")
      .HasColumnType("timestamp with time zone");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(message => message.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_outbox_message_company");
  }
}
