using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class ProvinceConfiguration
  : IEntityTypeConfiguration<Province>
{
  public void Configure(EntityTypeBuilder<Province> builder)
  {
    builder.ToTable(
      "province",
      table => table.HasCheckConstraint(
        "ck_province_status",
        "status IN ('Active', 'Inactive')"));

    builder.HasKey(province => province.ProvinceId)
      .HasName("pk_province");

    builder.Property(province => province.ProvinceId)
      .HasColumnName("province_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(province => province.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(province => province.Code)
      .HasColumnName("code")
      .HasMaxLength(30)
      .IsRequired();

    builder.Property(province => province.Name)
      .HasColumnName("name")
      .HasMaxLength(120)
      .IsRequired();

    builder.Property(province => province.Status)
      .HasColumnName("status")
      .HasMaxLength(20)
      .IsRequired();

    builder.Property(province => province.CreatedAt)
      .HasColumnName("created_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.HasIndex(province => new
    {
      province.CompanyId,
      province.Code
    })
      .IsUnique()
      .HasDatabaseName("uq_province_company_code");

    builder.HasIndex(province => new
    {
      province.CompanyId,
      province.Name
    })
      .IsUnique()
      .HasDatabaseName("uq_province_company_name");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(province => province.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_province_company");
  }
}