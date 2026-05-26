using LocalGo.Application.Common;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;

namespace LocalGo.Tests;

public sealed class ProgressHelperTests
{
    [Fact]
    public void Touch_updates_progress_and_updated_timestamps()
    {
        var before = DateTime.UtcNow.AddMinutes(-5);
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Title = "Test",
            Latitude = 13.75,
            Longitude = 100.5,
            SearchRadiusMeters = 3000,
            Status = ServiceRequestStatus.Open,
            LastProgressAt = before,
            CreatedAt = before,
            UpdatedAt = before,
        };

        ProgressHelper.Touch(request);

        Assert.True(request.LastProgressAt > before);
        Assert.True(request.UpdatedAt > before);
    }
}
