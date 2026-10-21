using LocalGo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations;

/// <summary>
/// Adds appointments.DeclineReason. Must carry [Migration] so MigrateAsync discovers it
/// (a Designer-less Migration without the attribute is skipped → 500 on SELECT DeclineReason).
/// </summary>
[DbContext(typeof(LocalGoDbContext))]
[Migration("20261021153000_AddAppointmentDeclineReason")]
public partial class AddAppointmentDeclineReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: safe if a previous deploy applied the column manually.
        migrationBuilder.Sql(
            """
            ALTER TABLE appointments
            ADD COLUMN IF NOT EXISTS "DeclineReason" character varying(2000) NULL;
            """);

        migrationBuilder.Sql(
            """
            UPDATE appointments
            SET "DeclineReason" = TRIM(BOTH FROM SUBSTRING("Note" FROM POSITION('เหตุผลที่ปฏิเสธ:' IN "Note") + CHAR_LENGTH('เหตุผลที่ปฏิเสธ:'))),
                "Note" = NULLIF(TRIM(BOTH FROM REGEXP_REPLACE("Note", E'\\n?เหตุผลที่ปฏิเสธ:.*$', '', 'n')), '')
            WHERE "Note" IS NOT NULL
              AND "Note" LIKE '%เหตุผลที่ปฏิเสธ:%'
              AND ("DeclineReason" IS NULL OR "DeclineReason" = '');
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE appointments
            SET "Note" = CASE
                WHEN "DeclineReason" IS NULL OR TRIM("DeclineReason") = '' THEN "Note"
                WHEN "Note" IS NULL OR TRIM("Note") = '' THEN 'เหตุผลที่ปฏิเสธ: ' || "DeclineReason"
                ELSE "Note" || E'\nเหตุผลที่ปฏิเสธ: ' || "DeclineReason"
            END
            WHERE "DeclineReason" IS NOT NULL AND TRIM("DeclineReason") <> '';
            """);

        migrationBuilder.Sql(
            """
            ALTER TABLE appointments DROP COLUMN IF EXISTS "DeclineReason";
            """);
    }
}
