using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_provider_services_ProviderId",
                table: "provider_services");

            migrationBuilder.DropIndex(
                name: "IX_appointments_ProviderId",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_Source_Status_CategoryId",
                table: "service_requests",
                columns: new[] { "Source", "Status", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_ProviderId_CategoryId_IsActive",
                table: "provider_services",
                columns: new[] { "ProviderId", "CategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ProviderId_Status",
                table: "appointments",
                columns: new[] { "ProviderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_service_requests_Source_Status_CategoryId",
                table: "service_requests");

            migrationBuilder.DropIndex(
                name: "IX_provider_services_ProviderId_CategoryId_IsActive",
                table: "provider_services");

            migrationBuilder.DropIndex(
                name: "IX_appointments_ProviderId_Status",
                table: "appointments");

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_ProviderId",
                table: "provider_services",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ProviderId",
                table: "appointments",
                column: "ProviderId");
        }
    }
}
