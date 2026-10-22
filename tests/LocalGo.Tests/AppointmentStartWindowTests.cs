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
    public void EnsureCanStartOrComplete_rejects_before_scheduled_time()
    {
        var scheduled = DateTime.UtcNow.AddMinutes(24);
        var ex = Assert.Throws<AppException>(() => AppointmentStartWindow.EnsureCanStartOrComplete(scheduled));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("เริ่มงานได้ตั้งแต่", ex.Message);
    }

    [Fact]
    public void EnsureCanStartOrComplete_allows_at_or_after_scheduled_time()
    {
        var scheduled = DateTime.UtcNow.AddMinutes(-1);
        AppointmentStartWindow.EnsureCanStartOrComplete(scheduled);
    }
}
