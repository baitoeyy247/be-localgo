using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalGo.Infrastructure.Persistence;

public static class AppointmentSchemaBootstrap
{
    public static async Task EnsureDeclineReasonColumnAsync(
        LocalGoDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE appointments
            ADD COLUMN IF NOT EXISTS "DeclineReason" character varying(2000) NULL;
            """,
            cancellationToken);

        try
        {
            _ = await db.Appointments.AsNoTracking()
                .Select(a => a.DeclineReason)
                .Take(1)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "appointments.DeclineReason column is still missing after bootstrap SQL");
            throw new InvalidOperationException(
                "appointments.DeclineReason column is missing — run migrations or ALTER TABLE on SIT database.",
                ex);
        }
    }

    /// <summary>Idempotent list indexes for admin appointment queries on SIT (avoids EF migration on cold start).</summary>
    public static async Task EnsureSitListIndexesAsync(
        LocalGoDbContext db,
        CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_appointments_CreatedAt"
                ON appointments ("CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_appointments_Status_CreatedAt"
                ON appointments ("Status", "CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_service_requests_CreatedAt"
                ON service_requests ("CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_service_requests_Status_CreatedAt"
                ON service_requests ("Status", "CreatedAt" DESC);
            """,
            cancellationToken);
    }
}
