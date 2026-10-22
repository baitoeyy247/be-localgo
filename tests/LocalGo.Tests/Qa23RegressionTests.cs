using LocalGo.Application.Common;
using LocalGo.Application.Services;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

/// <summary>QA round 23 — BE guardrails for start window and category mismatch.</summary>
public sealed class Qa23RegressionTests
{
    [Fact]
    public void B1_start_window_rejects_too_early_with_thai_message()
    {
        var ex = Assert.Throws<AppException>(() =>
            AppointmentStartWindow.EnsureCanStartOrComplete(DateTime.UtcNow.AddDays(1)));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("เริ่มงานได้ตั้งแต่", ex.Message);
    }

    [Fact]
    public async Task B2_bid_form_rejects_wrong_category_with_code()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedOpenMatchingNotificationAsync(db);
        var sut = new ServiceRequestAppService(db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);
        var request = await sut.CreateAsync(
            seed.Requester.Id,
            new LocalGo.Application.Dtos.CreateServiceRequestDto(
                seed.Category.Id,
                "QA23 wrong category",
                null,
                "Bangkok",
                seed.RequestLatitude,
                seed.RequestLongitude,
                seed.SearchRadiusMeters,
                DateTime.UtcNow.AddDays(1),
                null,
                "500",
                Publish: true),
            CancellationToken.None);

        var form = await sut.GetBidFormAsync(seed.WrongCategoryProviderUser.Id, request.Id, CancellationToken.None);

        Assert.False(form.Allowed);
        Assert.Equal("CATEGORY_MISMATCH", form.ReasonCode);
    }
}
