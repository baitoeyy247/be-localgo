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
}
