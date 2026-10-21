using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Auth;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalGo.Tests;

public sealed class Qa18AccessTests
{
    [Fact]
    public async Task BookFromProviderService_rejects_self_booking()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var owner = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = "dev-persona-prov-self",
            DisplayName = "Owner",
            CreatedAt = now,
            UpdatedAt = now,
        };
        var category = new ServiceCategory
        {
            Id = Guid.NewGuid(),
            Name = "Cat",
            Slug = $"cat-{Guid.NewGuid():N}",
            SortOrder = 1,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner.Id,
            Name = "My Shop",
            ProviderType = ProviderType.Individual,
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        var service = new ProviderService
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CategoryId = category.Id,
            Title = "Service",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Users.Add(owner);
        db.ServiceCategories.Add(category);
        db.Providers.Add(provider);
        db.ProviderServices.Add(service);
        await db.SaveChangesAsync();

        var sut = new AppointmentAppService(
            db,
            TestDbFactory.NoopNotifications(),
            NullLogger<AppointmentAppService>.Instance);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.BookFromProviderServiceAsync(
                owner.Id,
                provider.Id,
                service.Id,
                new CreateAppointmentRequest(
                    now.AddDays(1),
                    "addr",
                    13.75,
                    100.5,
                    null),
                CancellationToken.None));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task DevLogin_rejects_suspended_user()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            LineUserId = "dev-persona-suspended-01",
            DisplayName = "Suspended",
            Status = UserStatus.Suspended,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new DevAuthService(db, new FakeJwt());
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.LoginAsync(
                new DevLoginRequest { PersonaKey = "suspended-01", DisplayName = "Suspended" },
                CancellationToken.None));

        Assert.Equal(403, ex.StatusCode);
    }

    private sealed class FakeJwt : IJwtTokenGenerator
    {
        public string GenerateToken(User user) => "test-token";
    }
}
