using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class ProvinceManagerAssignmentConfiguration
  : IEntityTypeConfiguration<ProvinceManagerAssignment>
{
  public void Configure(
    EntityTypeBuilder<ProvinceManagerAssignment> builder)
  {
    builder.ToTable(
      "province_manager_assignment",
      table => table.HasCheckConstraint(
        "ck_province_manager_assignment_dates",
        "ends_at IS NULL OR ends_at > starts_at"));

    builder.HasKey(assignment => assignment.AssignmentId)
      .HasName("pk_province_manager_assignment");

    builder.Property(assignment => assignment.AssignmentId)
      .HasColumnName("assignment_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(assignment => assignment.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.ProvinceId)
      .HasColumnName("province_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.AreaManagerId)
      .HasColumnName("area_manager_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(assignment => assignment.ReportsToAdminId)
      .HasColumnName("reports_to_admin_id")
      .HasColumnType("uuid");

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

    builder.HasIndex(assignment => assignment.ProvinceId)
      .IsUnique()
      .HasFilter("\"ends_at\" IS NULL")
      .HasDatabaseName(
        "uq_province_manager_assignment_active_province");

    builder.HasIndex(assignment => assignment.AreaManagerId)
      .HasDatabaseName(
        "ix_province_manager_assignment_area_manager");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(assignment => assignment.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_province_manager_assignment_company");

    builder.HasOne<Province>()
      .WithMany()
      .HasForeignKey(assignment => assignment.ProvinceId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_province_manager_assignment_province");

    builder.HasOne<StaffProfile>()
      .WithMany()
      .HasForeignKey(assignment => assignment.AreaManagerId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_province_manager_assignment_area_manager");

    builder.HasOne<StaffProfile>()
      .WithMany()
      .HasForeignKey(assignment => assignment.ReportsToAdminId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName(
        "fk_province_manager_assignment_reports_to_admin");
  }
}
