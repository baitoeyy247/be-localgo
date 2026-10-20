using System.Security.Claims;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Infrastructure.Auth;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class FieldRulesTests
{
    [Theory]
    [InlineData("0612345678")]
    [InlineData("0812345678")]
    [InlineData("")]
    [InlineData(null)]
    public void Thai_mobile_accepts_empty_or_ten_digits_starting_with_zero(string? phone)
    {
        FieldRules.RequireThaiMobile(phone);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("1234567890")]
    [InlineData("061234567")]
    [InlineData("06123456789")]
    public void Thai_mobile_rejects_invalid_values(string phone)
    {
        var ex = Assert.Throws<AppException>(() => FieldRules.RequireThaiMobile(phone));
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("เปิดรับราคา")]
    [InlineData("1")]
    [InlineData("1000000")]
    public void Budget_accepts_open_or_in_range_integer(string budget)
    {
        FieldRules.RequireBudget(budget);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1000001")]
    [InlineData("12.5")]
    [InlineData("")]
    public void Budget_rejects_invalid_values(string budget)
    {
        var ex = Assert.Throws<AppException>(() => FieldRules.RequireBudget(budget));
        Assert.Equal(400, ex.StatusCode);
    }

    [Theory]
    [InlineData("40")]
    [InlineData("40-80")]
    [InlineData("40 – 80")]
    public void Price_accepts_number_or_range(string price)
    {
        FieldRules.RequirePrice(price);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("1.5")]
    [InlineData("80-40")]
    public void Price_rejects_invalid_values(string price)
    {
        var ex = Assert.Throws<AppException>(() => FieldRules.RequirePrice(price));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Preferred_time_accepts_a_future_start_and_later_end()
    {
        var now = new DateTime(2026, 10, 21, 3, 0, 0, DateTimeKind.Utc);
        var (start, end) = FieldRules.RequirePreferredTime(now.AddHours(2), now.AddHours(4), now);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal(now.AddHours(2), start);
        Assert.Equal(now.AddHours(4), end);
    }

    [Fact]
    public void Preferred_time_rejects_a_missing_or_past_start()
    {
        var now = new DateTime(2026, 10, 21, 3, 0, 0, DateTimeKind.Utc);
        var missing = Assert.Throws<AppException>(() => FieldRules.RequirePreferredTime(null, null, now));
        Assert.Equal(400, missing.StatusCode);
        Assert.Contains("กรุณาเลือกวันที่และเวลาที่ต้องการ", missing.Message);

        var past = Assert.Throws<AppException>(() => FieldRules.RequirePreferredTime(now.AddHours(-2), null, now));
        Assert.Equal(400, past.StatusCode);
        Assert.Contains("ไม่เป็นเวลาในอดีต", past.Message);

        var endedFirst = Assert.Throws<AppException>(() =>
            FieldRules.RequirePreferredTime(now.AddHours(4), now.AddHours(2), now));
        Assert.Equal(400, endedFirst.StatusCode);
        Assert.Contains("เวลาสิ้นสุด", endedFirst.Message);
    }

    [Fact]
    public async Task Create_request_returns_400_for_a_past_preferred_time()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = new ServiceRequestAppService(db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(
            Guid.NewGuid(),
            new CreateServiceRequestDto(
                Guid.NewGuid(),
                "งาน",
                null,
                null,
                13.7,
                100.5,
                5000,
                DateTime.UtcNow.AddHours(-3),
                null,
                "เปิดรับราคา",
                true),
            CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("ไม่เป็นเวลาในอดีต", ex.Message);
        Assert.Empty(db.ServiceRequests);
    }

    [Fact]
    public async Task Create_request_returns_400_for_invalid_budget()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = new ServiceRequestAppService(db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(
            Guid.NewGuid(),
            new CreateServiceRequestDto(Guid.NewGuid(), "งาน", null, null, 13.7, 100.5, 5000, null, null, "abc", true),
            CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.ServiceRequests);
    }

    [Fact]
    public async Task Create_provider_returns_400_for_invalid_phone()
    {
        await using var db = TestDbFactory.CreateContext();
        var sut = new ProviderAppService(db);
        var ex = await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(
            Guid.NewGuid(),
            new CreateProviderRequest("Shop", "ร้าน", null, null, "abc", null),
            CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Providers);
    }

    [Fact]
    public void Non_admin_principal_is_not_admin()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("systemRole", "User"), new Claim("role", "User")],
            authenticationType: "Bearer"));
        Assert.False(AdminAccess.IsAdmin(user));
    }

    [Fact]
    public void Admin_principal_is_admin()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("systemRole", "Admin")],
            authenticationType: "Bearer"));
        Assert.True(AdminAccess.IsAdmin(user));
    }
}
