using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRequestSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderServiceId",
                table: "service_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "service_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_ProviderServiceId",
                table: "service_requests",
                column: "ProviderServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_Source",
                table: "service_requests",
                column: "Source");

            migrationBuilder.AddForeignKey(
                name: "FK_service_requests_provider_services_ProviderServiceId",
                table: "service_requests",
                column: "ProviderServiceId",
                principalTable: "provider_services",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_provider_services_ProviderServiceId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_service_requests_ProviderServiceId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_service_requests_Source",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "ProviderServiceId",
                table: "service_requests");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "service_requests");
        }
    }
}
