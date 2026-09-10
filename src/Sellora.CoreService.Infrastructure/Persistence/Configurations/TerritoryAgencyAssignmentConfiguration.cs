using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class TerritoryAgencyAssignmentConfiguration
  : IEntityTypeConfiguration<TerritoryAgencyAssignment>
{
  public void Configure(
    EntityTypeBuilder<TerritoryAgencyAssignment> builder)
  {
    builder.ToTable(
      "territory_agency_assignment",
      table => table.HasCheckConstraint(
        "ck_territory_agency_assignment_dates",
        "ends_at IS NULL OR ends_at > starts_at"));

    builder.HasKey(assignment => assignment.AssignmentId)
      .HasName("pk_territory_agency_assignment");

    builder.Property(assignment => assignment.AssignmentId)
      .HasColumnName("assignment_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(assignment => assignment.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.TerritoryId)
      .HasColumnName("territory_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.AgencyId)
      .HasColumnName("agency_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.StartsAt)
      .HasColumnName("starts_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.Property(assignment => assignment.EndsAt)
      .HasColumnName("ends_at")
      .HasColumnType("timestamp with time zone");

    builder.Property(assignment => assignment.CreatedBy)
      .HasColumnName("created_by")
      .HasMaxLength(255)
      .IsRequired();

    builder.HasIndex(assignment => assignment.TerritoryId)
      .IsUnique()
      .HasFilter("\"ends_at\" IS NULL")
      .HasDatabaseName(
        "uq_territory_agency_assignment_active_territory");

    builder.HasIndex(assignment => assignment.AgencyId)
      .HasDatabaseName(
        "ix_territory_agency_assignment_agency");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(assignment => assignment.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_territory_agency_assignment_company");

    builder.HasOne<Territory>()
      .WithMany()
      .HasForeignKey(assignment => assignment.TerritoryId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_territory_agency_assignment_territory");

    builder.HasOne<Agency>()
      .WithMany()
      .HasForeignKey(assignment => assignment.AgencyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_territory_agency_assignment_agency");
  }
}
