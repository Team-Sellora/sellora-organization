using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellora.CoreService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrganizationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_company", x => x.company_id);
                    table.CheckConstraint("ck_company_status", "status IN ('Active', 'Inactive')");
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                columns: table => new
                {
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message", x => x.outbox_id);
                    table.ForeignKey(
                        name: "fk_outbox_message_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "province",
                columns: table => new
                {
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_province", x => x.province_id);
                    table.CheckConstraint("ck_province_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_province_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_profile",
                columns: table => new
                {
                    staff_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_profile", x => x.staff_profile_id);
                    table.CheckConstraint("ck_staff_profile_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_staff_profile_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agency",
                columns: table => new
                {
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agency", x => x.agency_id);
                    table.CheckConstraint("ck_agency_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_agency_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agency_province",
                        column: x => x.province_id,
                        principalTable: "province",
                        principalColumn: "province_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "territory",
                columns: table => new
                {
                    territory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    geographic_description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_territory", x => x.territory_id);
                    table.CheckConstraint("ck_territory_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_territory_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_territory_province",
                        column: x => x.province_id,
                        principalTable: "province",
                        principalColumn: "province_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "province_manager_assignment",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    province_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reports_to_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_province_manager_assignment", x => x.assignment_id);
                    table.CheckConstraint("ck_province_manager_assignment_dates", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_province_manager_assignment_area_manager",
                        column: x => x.area_manager_id,
                        principalTable: "staff_profile",
                        principalColumn: "staff_profile_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_province_manager_assignment_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_province_manager_assignment_province",
                        column: x => x.province_id,
                        principalTable: "province",
                        principalColumn: "province_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_province_manager_assignment_reports_to_admin",
                        column: x => x.reports_to_admin_id,
                        principalTable: "staff_profile",
                        principalColumn: "staff_profile_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agency_operator_assignment",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agency_operator_assignment", x => x.assignment_id);
                    table.CheckConstraint("ck_agency_operator_assignment_dates", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_agency_operator_assignment_agency",
                        column: x => x.agency_id,
                        principalTable: "agency",
                        principalColumn: "agency_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agency_operator_assignment_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agency_operator_assignment_operator",
                        column: x => x.operator_id,
                        principalTable: "staff_profile",
                        principalColumn: "staff_profile_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_rep_territory_assignment",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    territory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_rep_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_rep_territory_assignment", x => x.assignment_id);
                    table.CheckConstraint("ck_sales_rep_territory_assignment_dates", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_sales_rep_territory_assignment_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_rep_territory_assignment_sales_rep",
                        column: x => x.sales_rep_id,
                        principalTable: "staff_profile",
                        principalColumn: "staff_profile_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_rep_territory_assignment_territory",
                        column: x => x.territory_id,
                        principalTable: "territory",
                        principalColumn: "territory_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    territory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    owner_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    owner_identity_sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    owner_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    owner_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    address = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop", x => x.shop_id);
                    table.CheckConstraint("ck_shop_credit_limit", "credit_limit >= 0");
                    table.CheckConstraint("ck_shop_latitude", "latitude >= -90 AND latitude <= 90");
                    table.CheckConstraint("ck_shop_longitude", "longitude >= -180 AND longitude <= 180");
                    table.CheckConstraint("ck_shop_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_shop_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shop_territory",
                        column: x => x.territory_id,
                        principalTable: "territory",
                        principalColumn: "territory_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "territory_agency_assignment",
                columns: table => new
                {
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    territory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_territory_agency_assignment", x => x.assignment_id);
                    table.CheckConstraint("ck_territory_agency_assignment_dates", "ends_at IS NULL OR ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_territory_agency_assignment_agency",
                        column: x => x.agency_id,
                        principalTable: "agency",
                        principalColumn: "agency_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_territory_agency_assignment_company",
                        column: x => x.company_id,
                        principalTable: "company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_territory_agency_assignment_territory",
                        column: x => x.territory_id,
                        principalTable: "territory",
                        principalColumn: "territory_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agency_company_id",
                table: "agency",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_agency_province_name",
                table: "agency",
                columns: new[] { "province_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agency_operator_assignment_agency",
                table: "agency_operator_assignment",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_agency_operator_assignment_company_id",
                table: "agency_operator_assignment",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_agency_operator_assignment_operator",
                table: "agency_operator_assignment",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "uq_company_tenant_code",
                table: "company",
                column: "tenant_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_company_id",
                table: "outbox_message",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_province_company_code",
                table: "province",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_province_company_name",
                table: "province",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_province_manager_assignment_area_manager",
                table: "province_manager_assignment",
                column: "area_manager_id");

            migrationBuilder.CreateIndex(
                name: "IX_province_manager_assignment_company_id",
                table: "province_manager_assignment",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_province_manager_assignment_reports_to_admin_id",
                table: "province_manager_assignment",
                column: "reports_to_admin_id");

            migrationBuilder.CreateIndex(
                name: "uq_province_manager_assignment_active_province",
                table: "province_manager_assignment",
                column: "province_id",
                unique: true,
                filter: "\"ends_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sales_rep_territory_assignment_company_id",
                table: "sales_rep_territory_assignment",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_sales_rep_assignment_active_rep",
                table: "sales_rep_territory_assignment",
                column: "sales_rep_id",
                unique: true,
                filter: "\"ends_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_sales_rep_assignment_active_territory",
                table: "sales_rep_territory_assignment",
                column: "territory_id",
                unique: true,
                filter: "\"ends_at\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_shop_company_id",
                table: "shop",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_shop_territory_id",
                table: "shop",
                column: "territory_id");

            migrationBuilder.CreateIndex(
                name: "uq_shop_owner_identity_sub",
                table: "shop",
                column: "owner_identity_sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_profile_company_id",
                table: "staff_profile",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_staff_profile_identity_sub",
                table: "staff_profile",
                column: "identity_sub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_territory_company_code",
                table: "territory",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_territory_province_name",
                table: "territory",
                columns: new[] { "province_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_territory_agency_assignment_agency",
                table: "territory_agency_assignment",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "IX_territory_agency_assignment_company_id",
                table: "territory_agency_assignment",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "uq_territory_agency_assignment_active_territory",
                table: "territory_agency_assignment",
                column: "territory_id",
                unique: true,
                filter: "\"ends_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agency_operator_assignment");

            migrationBuilder.DropTable(
                name: "outbox_message");

            migrationBuilder.DropTable(
                name: "province_manager_assignment");

            migrationBuilder.DropTable(
                name: "sales_rep_territory_assignment");

            migrationBuilder.DropTable(
                name: "shop");

            migrationBuilder.DropTable(
                name: "territory_agency_assignment");

            migrationBuilder.DropTable(
                name: "staff_profile");

            migrationBuilder.DropTable(
                name: "agency");

            migrationBuilder.DropTable(
                name: "territory");

            migrationBuilder.DropTable(
                name: "province");

            migrationBuilder.DropTable(
                name: "company");
        }
    }
}
