using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class AgencyConfiguration : IEntityTypeConfiguration<Agency>
{
  public void Configure(EntityTypeBuilder<Agency> builder)
  {
    builder.ToTable(
      "agency",
      table => table.HasCheckConstraint(
        "ck_agency_status",
        "status IN ('Active', 'Inactive')"));

    builder.HasKey(agency => agency.AgencyId)
      .HasName("pk_agency");

    builder.Property(agency => agency.AgencyId)
      .HasColumnName("agency_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(agency => agency.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(agency => agency.ProvinceId)
      .HasColumnName("province_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(agency => agency.Name)
      .HasColumnName("name")
      .HasMaxLength(160)
      .IsRequired();

    builder.Property(agency => agency.Email)
      .HasColumnName("email")
      .HasMaxLength(320);

    builder.Property(agency => agency.Phone)
      .HasColumnName("phone")
      .HasMaxLength(40);

    builder.Property(agency => agency.Address)
      .HasColumnName("address")
      .HasColumnType("text");

    builder.Property(agency => agency.Status)
      .HasColumnName("status")
      .HasMaxLength(20)
      .IsRequired();

    builder.Property(agency => agency.CreatedAt)
      .HasColumnName("created_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.HasIndex(agency => new
    {
      agency.ProvinceId,
      agency.Name
    })
      .IsUnique()
      .HasDatabaseName("uq_agency_province_name");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(agency => agency.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_agency_company");

    builder.HasOne<Province>()
      .WithMany()
      .HasForeignKey(agency => agency.ProvinceId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_agency_province");
  }
}
