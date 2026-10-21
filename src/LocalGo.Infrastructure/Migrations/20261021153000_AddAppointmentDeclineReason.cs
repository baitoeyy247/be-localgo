using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations;

public class AddAppointmentDeclineReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeclineReason",
            table: "appointments",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        // Split legacy "เหตุผลที่ปฏิเสธ: …" suffixes out of Note into DeclineReason.
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

        migrationBuilder.DropColumn(
            name: "DeclineReason",
            table: "appointments");
    }
}
