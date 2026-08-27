using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellora.CoreService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShopChangeAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_credit_limit",
                table: "shop");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_latitude",
                table: "shop");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_longitude",
                table: "shop");

            migrationBuilder.CreateTable(
                name: "audit_entry",
                columns: table => new
                {
                    audit_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    old_value = table.Column<string>(type: "jsonb", nullable: false),
                    new_value = table.Column<string>(type: "jsonb", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entry", x => x.audit_entry_id);
                    table.ForeignKey(
                        name: "fk_audit_entry_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_credit_limit",
                table: "shop",
                sql: "CAST(credit_limit AS NUMERIC) >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_latitude",
                table: "shop",
                sql: "CAST(latitude AS NUMERIC) >= -90 AND CAST(latitude AS NUMERIC) <= 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_longitude",
                table: "shop",
                sql: "CAST(longitude AS NUMERIC) >= -180 AND CAST(longitude AS NUMERIC) <= 180");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entry_company_id",
                table: "audit_entry",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_entity",
                table: "audit_entry",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_credit_limit",
                table: "shop");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_latitude",
                table: "shop");

            migrationBuilder.DropCheckConstraint(
                name: "ck_shop_longitude",
                table: "shop");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_credit_limit",
                table: "shop",
                sql: "credit_limit >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_latitude",
                table: "shop",
                sql: "latitude >= -90 AND latitude <= 90");

            migrationBuilder.AddCheckConstraint(
                name: "ck_shop_longitude",
                table: "shop",
                sql: "longitude >= -180 AND longitude <= 180");
        }
    }
}
