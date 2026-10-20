using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueReviewPerRequestReviewer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_ServiceRequestId",
                table: "reviews");

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY "ServiceRequestId", "ReviewerUserId"
                               ORDER BY "CreatedAt", "Id"
                           ) AS rn
                    FROM reviews
                    WHERE "ServiceRequestId" IS NOT NULL
                )
                DELETE FROM reviews
                WHERE "Id" IN (SELECT "Id" FROM ranked WHERE rn > 1);

                UPDATE providers AS p
                SET "VerifiedReviewCount" = counts.verified_count,
                    "PublicReviewCount" = counts.public_count,
                    "AverageVerifiedRating" = counts.verified_avg,
                    "AveragePublicRating" = counts.public_avg,
                    "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
                FROM (
                    SELECT provider_ids."Id" AS provider_id,
                           COALESCE(SUM(CASE WHEN r."ReviewType" = 0 AND r."Status" = 0 THEN 1 ELSE 0 END), 0)::int AS verified_count,
                           COALESCE(SUM(CASE WHEN r."ReviewType" = 1 AND r."Status" = 0 THEN 1 ELSE 0 END), 0)::int AS public_count,
                           COALESCE(AVG(CASE WHEN r."ReviewType" = 0 AND r."Status" = 0 THEN r."Rating" END), 0)::numeric AS verified_avg,
                           COALESCE(AVG(CASE WHEN r."ReviewType" = 1 AND r."Status" = 0 THEN r."Rating" END), 0)::numeric AS public_avg
                    FROM providers AS provider_ids
                    LEFT JOIN reviews AS r ON r."ProviderId" = provider_ids."Id"
                    GROUP BY provider_ids."Id"
                ) AS counts
                WHERE p."Id" = counts.provider_id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ServiceRequestId_ReviewerUserId",
                table: "reviews",
                columns: new[] { "ServiceRequestId", "ReviewerUserId" },
                unique: true,
                filter: "\"ServiceRequestId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_ServiceRequestId_ReviewerUserId",
                table: "reviews");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ServiceRequestId",
                table: "reviews",
                column: "ServiceRequestId");
        }
    }
}
