using LocalGo.Application.Common;

namespace LocalGo.Tests;

public sealed class AppointmentStartWindowTests
{
    [Fact]
    public void EnsureCanStartOrComplete_throws_with_thai_earliest_label()
    {
        var scheduled = DateTime.UtcNow.AddDays(2);
        var ex = Assert.Throws<AppException>(() => AppointmentStartWindow.EnsureCanStartOrComplete(scheduled));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("เริ่มงานได้ตั้งแต่", ex.Message);
    }

    [Fact]
    public void EnsureCanStartOrComplete_allows_within_30_minutes_before()
    {
        var scheduled = DateTime.UtcNow.AddMinutes(20);
        AppointmentStartWindow.EnsureCanStartOrComplete(scheduled);
    }
}
