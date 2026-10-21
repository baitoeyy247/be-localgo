using System.Globalization;

namespace LocalGo.Application.Common;

/// <summary>Provider may start/complete work shortly before the scheduled appointment time.</summary>
public static class AppointmentStartWindow
{
    public static readonly TimeSpan EarlyStartAllowed = TimeSpan.FromMinutes(30);

    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public static DateTime EarliestStartUtc(DateTime scheduledAt) =>
        ToUtc(scheduledAt).Subtract(EarlyStartAllowed);

    public static void EnsureCanStartOrComplete(DateTime? scheduledAt)
    {
        if (scheduledAt is null)
        {
            throw new AppException("ยังไม่ถึงเวลานัด จึงเริ่มหรือปิดงานไม่ได้", 409);
        }

        var earliest = EarliestStartUtc(scheduledAt.Value);
        if (DateTime.UtcNow < earliest)
        {
            var label = FormatThaiDateTime(earliest);
            throw new AppException($"เริ่มงานได้ตั้งแต่ {label}", 409);
        }
    }

    public static string FormatThaiDateTime(DateTime utc)
    {
        var local = utc.AddHours(7);
        return local.ToString("d MMM yyyy HH:mm", new CultureInfo("th-TH")) + " น.";
    }
}
