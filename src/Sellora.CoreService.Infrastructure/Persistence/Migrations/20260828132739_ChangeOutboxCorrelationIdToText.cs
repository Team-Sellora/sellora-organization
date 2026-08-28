using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sellora.CoreService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOutboxCorrelationIdToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "correlation_id",
                table: "outbox_message",
                type: "text",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "correlation_id",
                table: "outbox_message",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 128);
        }
    }
}
