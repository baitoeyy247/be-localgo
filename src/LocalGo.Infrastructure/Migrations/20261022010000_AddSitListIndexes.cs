using LocalGo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalGo.Infrastructure.Migrations;

[DbContext(typeof(LocalGoDbContext))]
[Migration("20261022010000_AddSitListIndexes")]
public partial class AddSitListIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_appointments_CreatedAt"
                ON appointments ("CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_appointments_Status_CreatedAt"
                ON appointments ("Status", "CreatedAt" DESC);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS "IX_appointments_Status_CreatedAt";
            DROP INDEX IF EXISTS "IX_appointments_CreatedAt";
            """);
    }
}
