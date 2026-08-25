using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class StaffProfileConfiguration
  : IEntityTypeConfiguration<StaffProfile>
{
  public void Configure(EntityTypeBuilder<StaffProfile> builder)
  {
    builder.ToTable(
      "staff_profile",
      table => table.HasCheckConstraint(
        "ck_staff_profile_status",
        "status IN ('Active', 'Inactive')"));

    builder.HasKey(profile => profile.StaffProfileId)
      .HasName("pk_staff_profile");

    builder.Property(profile => profile.StaffProfileId)
      .HasColumnName("staff_profile_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(profile => profile.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(profile => profile.IdentitySub)
      .HasColumnName("identity_sub")
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(profile => profile.Role)
      .HasColumnName("role")
      .HasMaxLength(40)
      .IsRequired();

    builder.Property(profile => profile.DisplayName)
      .HasColumnName("display_name")
      .HasMaxLength(200)
      .IsRequired();

    builder.Property(profile => profile.Email)
      .HasColumnName("email")
      .HasMaxLength(320);

    builder.Property(profile => profile.Phone)
      .HasColumnName("phone")
      .HasMaxLength(40);

    builder.Property(profile => profile.Status)
      .HasColumnName("status")
      .HasMaxLength(20)
      .IsRequired();

    builder.Property(profile => profile.CreatedAt)
      .HasColumnName("created_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.HasIndex(profile => profile.IdentitySub)
      .IsUnique()
      .HasDatabaseName("uq_staff_profile_identity_sub");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(profile => profile.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_staff_profile_company");
  }
}