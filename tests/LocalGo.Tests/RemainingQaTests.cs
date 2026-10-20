using System.Reflection;
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
}
