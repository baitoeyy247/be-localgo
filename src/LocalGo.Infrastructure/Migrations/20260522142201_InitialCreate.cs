using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace LocalGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerType = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "service_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_categories_service_categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LineUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PictureUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SystemRole = table.Column<int>(type: "integer", nullable: false),
                    ActiveRole = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PrivacyNoticeAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrivacyNoticeVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "admin_action_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_action_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_action_logs_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    LineUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContactLineId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ContactUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AverageVerifiedRating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    AveragePublicRating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    VerifiedReviewCount = table.Column<int>(type: "integer", nullable: false),
                    PublicReviewCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_providers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_providers_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_users_ReporterUserId",
                        column: x => x.ReporterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AddressText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<Point>(type: "geography (Point,4326)", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ServiceRadiusMeters = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_branches_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BasePriceText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_services_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_provider_services_service_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_service_branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_service_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_service_branches_provider_branches_ProviderBranchId",
                        column: x => x.ProviderBranchId,
                        principalTable: "provider_branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_provider_service_branches_provider_services_ProviderService~",
                        column: x => x.ProviderServiceId,
                        principalTable: "provider_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddressText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<Point>(type: "geography (Point,4326)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appointments_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_appointments_users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PriceText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bids_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AddressText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Location = table.Column<Point>(type: "geography (Point,4326)", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    SearchRadiusMeters = table.Column<int>(type: "integer", nullable: false),
                    PreferredStartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PreferredEndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BudgetText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FirstBidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastProgressAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SelectedBidId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_requests_bids_SelectedBidId",
                        column: x => x.SelectedBidId,
                        principalTable: "bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_service_requests_providers_SelectedProviderId",
                        column: x => x.SelectedProviderId,
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_service_requests_service_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_requests_users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewType = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reviews_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reviews_service_requests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "service_requests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_reviews_users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_action_logs_AdminUserId",
                table: "admin_action_logs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ProviderId",
                table: "appointments",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_RequesterUserId",
                table: "appointments",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_ServiceRequestId",
                table: "appointments",
                column: "ServiceRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bids_ProviderId",
                table: "bids",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_bids_ServiceRequestId_ProviderId",
                table: "bids",
                columns: new[] { "ServiceRequestId", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_OwnerType_OwnerId",
                table: "media_assets",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_logs_Status_CreatedAt",
                table: "notification_logs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_logs_UserId",
                table: "notification_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_branches_Location",
                table: "provider_branches",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_provider_branches_ProviderId",
                table: "provider_branches",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_service_branches_ProviderBranchId",
                table: "provider_service_branches",
                column: "ProviderBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_service_branches_ProviderServiceId_ProviderBranchId",
                table: "provider_service_branches",
                columns: new[] { "ProviderServiceId", "ProviderBranchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_CategoryId",
                table: "provider_services",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_ProviderId",
                table: "provider_services",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_providers_OwnerUserId",
                table: "providers",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_ReporterUserId",
                table: "reports",
                column: "ReporterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ProviderId",
                table: "reviews",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ReviewerUserId",
                table: "reviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ServiceRequestId",
                table: "reviews",
                column: "ServiceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_ParentId",
                table: "service_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_service_categories_Slug",
                table: "service_categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_CategoryId",
                table: "service_requests",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_Location",
                table: "service_requests",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_RequesterUserId",
                table: "service_requests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_SelectedBidId",
                table: "service_requests",
                column: "SelectedBidId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_SelectedProviderId",
                table: "service_requests",
                column: "SelectedProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_service_requests_Status_LastProgressAt",
                table: "service_requests",
                columns: new[] { "Status", "LastProgressAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_LineUserId",
                table: "users",
                column: "LineUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_service_requests_ServiceRequestId",
                table: "appointments",
                column: "ServiceRequestId",
                principalTable: "service_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bids_service_requests_ServiceRequestId",
                table: "bids",
                column: "ServiceRequestId",
                principalTable: "service_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_providers_users_OwnerUserId",
                table: "providers");

            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_users_RequesterUserId",
                table: "service_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_bids_providers_ProviderId",
                table: "bids");

            migrationBuilder.DropForeignKey(
                name: "FK_service_requests_providers_SelectedProviderId",
                table: "service_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_bids_service_requests_ServiceRequestId",
                table: "bids");

            migrationBuilder.DropTable(
                name: "admin_action_logs");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropTable(
                name: "notification_logs");

            migrationBuilder.DropTable(
                name: "provider_service_branches");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "provider_branches");

            migrationBuilder.DropTable(
                name: "provider_services");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "providers");

            migrationBuilder.DropTable(
                name: "service_requests");

            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropTable(
                name: "service_categories");
        }
    }
}
