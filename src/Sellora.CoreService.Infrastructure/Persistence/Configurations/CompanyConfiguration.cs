using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
  public void Configure(EntityTypeBuilder<Company> builder)
  {
    builder.ToTable(
      "company",
      table => table.HasCheckConstraint(
        "ck_company_status",
        "status IN ('Active', 'Inactive')"));

    builder.HasKey(company => company.CompanyId)
      .HasName("pk_company");

    builder.Property(company => company.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(company => company.TenantCode)
      .HasColumnName("tenant_code")
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(company => company.Name)
      .HasColumnName("name")
      .HasMaxLength(200)
      .IsRequired();

    builder.Property(company => company.Status)
      .HasColumnName("status")
      .HasMaxLength(20)
      .IsRequired();

    builder.Property(company => company.CreatedAt)
      .HasColumnName("created_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.Property(company => company.UpdatedAt)
      .HasColumnName("updated_at")
      .HasColumnType("timestamp with time zone");

    builder.HasIndex(company => company.TenantCode)
      .IsUnique()
      .HasDatabaseName("uq_company_tenant_code");
  }
}