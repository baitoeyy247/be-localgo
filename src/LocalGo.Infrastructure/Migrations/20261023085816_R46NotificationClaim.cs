using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class R46NotificationClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "notification_logs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "notification_logs");
        }
    }
}
