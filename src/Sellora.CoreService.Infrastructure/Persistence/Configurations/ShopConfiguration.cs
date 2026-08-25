using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Persistence.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
  public void Configure(EntityTypeBuilder<Shop> builder)
  {
    builder.ToTable(
      "shop",
      table =>
      {
        table.HasCheckConstraint(
          "ck_shop_status",
          "status IN ('Active', 'Inactive')");

        table.HasCheckConstraint(
          "ck_shop_latitude",
          "latitude >= -90 AND latitude <= 90");

        table.HasCheckConstraint(
          "ck_shop_longitude",
          "longitude >= -180 AND longitude <= 180");

        table.HasCheckConstraint(
          "ck_shop_credit_limit",
          "credit_limit >= 0");
      });

    builder.HasKey(shop => shop.ShopId)
      .HasName("pk_shop");

    builder.Property(shop => shop.ShopId)
      .HasColumnName("shop_id")
      .HasColumnType("uuid")
      .ValueGeneratedNever();

    builder.Property(shop => shop.CompanyId)
      .HasColumnName("company_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(shop => shop.TerritoryId)
      .HasColumnName("territory_id")
      .HasColumnType("uuid")
      .IsRequired();

    builder.Property(shop => shop.Name)
      .HasColumnName("name")
      .HasMaxLength(200)
      .IsRequired();

    builder.Property(shop => shop.OwnerName)
      .HasColumnName("owner_name")
      .HasMaxLength(200);

    builder.Property(shop => shop.OwnerIdentitySub)
      .HasColumnName("owner_identity_sub")
      .HasMaxLength(255);

    builder.Property(shop => shop.OwnerEmail)
      .HasColumnName("owner_email")
      .HasMaxLength(320);

    builder.Property(shop => shop.OwnerPhone)
      .HasColumnName("owner_phone")
      .HasMaxLength(40);

    builder.Property(shop => shop.Address)
      .HasColumnName("address")
      .HasColumnType("text")
      .IsRequired();

    builder.Property(shop => shop.Latitude)
      .HasColumnName("latitude")
      .HasPrecision(9, 6)
      .IsRequired();

    builder.Property(shop => shop.Longitude)
      .HasColumnName("longitude")
      .HasPrecision(9, 6)
      .IsRequired();

    builder.Property(shop => shop.CreditLimit)
      .HasColumnName("credit_limit")
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(shop => shop.Status)
      .HasColumnName("status")
      .HasMaxLength(20)
      .IsRequired();

    builder.Property(shop => shop.CreatedAt)
      .HasColumnName("created_at")
      .HasColumnType("timestamp with time zone")
      .IsRequired();

    builder.Property(shop => shop.UpdatedAt)
      .HasColumnName("updated_at")
      .HasColumnType("timestamp with time zone");

    builder.HasIndex(shop => shop.OwnerIdentitySub)
      .IsUnique()
      .HasDatabaseName("uq_shop_owner_identity_sub");

    builder.HasOne<Company>()
      .WithMany()
      .HasForeignKey(shop => shop.CompanyId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_shop_company");

    builder.HasOne<Territory>()
      .WithMany()
      .HasForeignKey(shop => shop.TerritoryId)
      .OnDelete(DeleteBehavior.Restrict)
      .HasConstraintName("fk_shop_territory");
  }
}
