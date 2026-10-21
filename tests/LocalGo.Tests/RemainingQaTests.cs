using System.Reflection;
using System.Text.Json;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos.Responses;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class RemainingQaTests
{
    [Fact]
    public void Review_response_dto_has_no_private_contact_fields()
    {
        var names = typeof(ReviewResponseDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains("Id", names);
        Assert.Contains("Rating", names);
        Assert.Contains("Comment", names);
        Assert.Contains("CreatedAt", names);
        Assert.Contains("ReviewerDisplayName", names);
        Assert.Contains("ServiceRequestId", names);
        Assert.DoesNotContain("ContactPhone", names);
        Assert.DoesNotContain("ContactLineId", names);
        Assert.DoesNotContain("OwnerUserId", names);
        Assert.DoesNotContain("LineUserId", names);
        Assert.DoesNotContain("PhoneNumber", names);
    }

    [Fact]
    public async Task ListUsers_returns_one_row_per_account()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-admin-{Guid.NewGuid():N}",
            DisplayName = "Demo Admin",
            SystemRole = SystemRole.Admin,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            ProviderType = ProviderType.Individual,
            Name = "Demo Admin",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.Add(user);
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        var rows = await new AdminAppService(db).ListUsersAsync(CancellationToken.None);
        var matches = rows.Where(row => row.Id == user.Id).ToList();

        Assert.Single(matches);
        Assert.Equal(rows.Select(row => row.Id).Distinct().Count(), rows.Count);
    }

    [Fact]
    public async Task ListMine_hides_scheduled_when_a_marketplace_job_has_no_bids()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = $"line-zero-{Guid.NewGuid():N}",
            DisplayName = "Zero Bids",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Zero",
            Slug = $"zero-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = user.Id,
            CategoryId = category.Id,
            Title = "Scheduled with no bids",
            Latitude = 13.7563,
            Longitude = 100.5018,
            Location = GeoHelper.CreatePoint(13.7563, 100.5018),
            SearchRadiusMeters = 5000,
            Status = ServiceRequestStatus.Scheduled,
            Source = ServiceRequestSource.Marketplace,
            LastProgressAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.Add(user);
        db.ServiceCategories.Add(category);
        db.ServiceRequests.Add(request);
        await db.SaveChangesAsync();

        var sut = new ServiceRequestAppService(db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);
        var mine = await sut.ListMineAsync(user.Id, CancellationToken.None);
        var row = Assert.Single(mine);

        Assert.Equal(ServiceRequestStatus.Open.ToString(), row.Status);
        Assert.Equal(0, row.BidCount);
    }

    [Fact]
    public void Public_provider_json_omits_contact_fields_when_they_are_null()
    {
        var dto = new ProviderResponseDto
        {
            Id = Guid.NewGuid(),
            ProviderType = "Individual",
            Name = "Public",
            Status = "Active",
        };
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("contactPhone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contactLineId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownerUserId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Start_before_the_appointment_window_is_rejected()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Appointment!.Status = AppointmentStatus.Confirmed;
        seed.Appointment.ScheduledAt = DateTime.UtcNow.AddDays(2);
        seed.Request.Status = ServiceRequestStatus.Scheduled;
        await db.SaveChangesAsync();

        var sut = new ServiceRequestAppService(db, TestDbFactory.NoopNotifications(), NullLogger<ServiceRequestAppService>.Instance);
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.StartAsync(seed.ProviderUser.Id, seed.Request.Id, CancellationToken.None));

        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("ยังไม่ถึงเวลานัด", ex.Message);
    }

    [Fact]
    public void Bid_message_over_2000_characters_is_rejected()
    {
        var ex = Assert.Throws<AppException>(() => FieldRules.RequireBidDescription(new string('ก', 2001)));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("2000", ex.Message);
    }

    [Fact]
    public async Task Contact_stays_hidden_until_the_appointment_is_confirmed()
    {
        await using var db = TestDbFactory.CreateContext();
        var seed = await TestDbFactory.SeedMarketplaceJobAsync(db, withPendingAppointment: true);
        seed.Provider.ContactPhone = "0812345678";
        seed.Provider.ContactLineId = "line-secret";
        await db.SaveChangesAsync();

        var sut = new ProviderAppService(db);
        Assert.False(await sut.ViewerMaySeeContactAsync(null, seed.Provider.Id, CancellationToken.None));
        Assert.False(await sut.ViewerMaySeeContactAsync(seed.Requester.Id, seed.Provider.Id, CancellationToken.None));
        Assert.True(await sut.ViewerMaySeeContactAsync(seed.ProviderUser.Id, seed.Provider.Id, CancellationToken.None));

        seed.Appointment!.Status = AppointmentStatus.Confirmed;
        await db.SaveChangesAsync();

        Assert.True(await sut.ViewerMaySeeContactAsync(seed.Requester.Id, seed.Provider.Id, CancellationToken.None));
    }
}
