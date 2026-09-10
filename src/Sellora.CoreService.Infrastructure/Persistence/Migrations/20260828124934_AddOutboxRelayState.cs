using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellora.CoreService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRelayState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "outbox_message",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "outbox_message",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_at",
                table: "outbox_message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                table: "outbox_message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                table: "outbox_message",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "schema_version",
                table: "outbox_message",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_pending_relay",
                table: "outbox_message",
                columns: new[] { "published_at", "next_attempt_at", "lease_expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_message_pending_relay",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "lease_expires_at",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "lease_id",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "outbox_message");
        }
    }
}
