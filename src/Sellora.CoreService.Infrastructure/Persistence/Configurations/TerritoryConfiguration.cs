using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class TerritoryConfiguration
  : IEntityTypeConfiguration<Territory>
{
  public void Configure(EntityTypeBuilder<Territory> builder)
  {
    builder.ToTable(
      "territory",
      table => table.HasCheckConstraint(
        "ck_territory_status",
        "status IN ('Active', 'Inactive')"));

    builder.HasKey(territory => territory.TerritoryId)
      .HasName("pk_territory");

    builder.Property(territory => territory.TerritoryId)
      .HasColumnName("territory_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(territory => territory.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(territory => territory.ProvinceId)
      .HasColumnName("province_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(territory => territory.Code)
      .HasColumnName("code")
      .HasMaxLength(40)
      .IsRequired();

    builder.Property(territory => territory.Name)
      .HasColumnName("name")
      .HasMaxLength(160)
      .IsRequired();

    builder.Property(territory => territory.GeographicDescription)
      .HasColumnName("geographic_description")
      .HasColumnType("text");

    builder.Property(territory => territory.Status)
      .HasColumnName("status")
      .HasMaxLength(20)
      .IsRequired();

    builder.Property(territory => territory.CreatedAt)
      .HasColumnName("created_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.HasIndex(territory => new
      {
        territory.CompanyId,
        territory.Code
      })
      .IsUnique()
      .HasDatabaseName("uq_territory_company_code");

    builder.HasIndex(territory => new
      {
        territory.ProvinceId,
        territory.Name
      })
      .IsUnique()
      .HasDatabaseName("uq_territory_province_name");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(territory => territory.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_territory_company");

    builder.HasOne<Province>()
      .WithMany()
      .HasForeignKey(territory => territory.ProvinceId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_territory_province");
  }
}
