using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class AgencyOperatorAssignmentConfiguration
  : IEntityTypeConfiguration<AgencyOperatorAssignment>
{
  public void Configure(
    EntityTypeBuilder<AgencyOperatorAssignment> builder)
  {
    builder.ToTable(
      "agency_operator_assignment",
      table => table.HasCheckConstraint(
        "ck_agency_operator_assignment_dates",
        "ends_at IS NULL OR ends_at > starts_at"));

    builder.HasKey(assignment => assignment.AssignmentId)
      .HasName("pk_agency_operator_assignment");

    builder.Property(assignment => assignment.AssignmentId)
      .HasColumnName("assignment_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(assignment => assignment.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.AgencyId)
      .HasColumnName("agency_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.OperatorId)
      .HasColumnName("operator_id")
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

    builder.HasIndex(assignment => assignment.AgencyId)
      .HasDatabaseName(
        "ix_agency_operator_assignment_agency");

    builder.HasIndex(assignment => assignment.OperatorId)
      .HasDatabaseName(
        "ix_agency_operator_assignment_operator");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(assignment => assignment.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_agency_operator_assignment_company");

    builder.HasOne<Agency>()
      .WithMany()
      .HasForeignKey(assignment => assignment.AgencyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_agency_operator_assignment_agency");

    builder.HasOne<StaffProfile>()
      .WithMany()
      .HasForeignKey(assignment => assignment.OperatorId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_agency_operator_assignment_operator");
  }
}
