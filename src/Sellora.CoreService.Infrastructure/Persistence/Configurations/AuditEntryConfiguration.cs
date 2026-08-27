using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration
  : IEntityTypeConfiguration<AuditEntry>
{
  public void Configure(EntityTypeBuilder<AuditEntry> builder)
  {
    builder.ToTable("audit_entry");

    builder.HasKey(entry => entry.AuditEntryId)
      .HasName("pk_audit_entry");

    builder.Property(entry => entry.AuditEntryId)
      .HasColumnName("audit_entry_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(entry => entry.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(entry => entry.EntityType)
      .HasColumnName("entity_type")
      .HasMaxLength(80)
      .IsRequired();

    builder.Property(entry => entry.EntityId)
      .HasColumnName("entity_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(entry => entry.FieldName)
      .HasColumnName("field_name")
      .HasMaxLength(80)
      .IsRequired();

    builder.Property(entry => entry.OldValue)
      .HasColumnName("old_value")
      .HasColumnType("jsonb")
      .IsRequired();

    builder.Property(entry => entry.NewValue)
      .HasColumnName("new_value")
      .HasColumnType("jsonb")
      .IsRequired();

    builder.Property(entry => entry.ChangedBy)
      .HasColumnName("changed_by")
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(entry => entry.ChangedAt)
      .HasColumnName("changed_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.HasIndex(entry => new { entry.EntityType, entry.EntityId })
      .HasDatabaseName("ix_audit_entry_entity");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(entry => entry.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_audit_entry_company");
  }
}