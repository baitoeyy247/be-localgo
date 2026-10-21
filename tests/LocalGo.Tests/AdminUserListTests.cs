using LocalGo.Application.Common;
using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;

namespace LocalGo.Tests;

public sealed class AdminUserListTests
{
    [Fact]
    public async Task SetUserStatus_rejects_self_suspend()
    {
        await using var db = TestDbFactory.CreateContext();
        var adminId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Users.Add(new User
        {
            Id = adminId,
            LineUserId = "dev-persona-admin-01",
            DisplayName = "Admin",
            SystemRole = SystemRole.Admin,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var sut = new AdminAppService(db, new ReviewAppService(db, TestDbFactory.NoopNotifications()));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            sut.SetUserStatusAsync(adminId, adminId, "Suspended", CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ListUsers_returns_each_account_once()
    {
        await using var db = TestDbFactory.CreateContext();
        var now = DateTime.UtcNow;
        db.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                LineUserId = "dev-admin-aaaaaa",
                DisplayName = "Demo Admin",
                SystemRole = SystemRole.Admin,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new User
            {
                Id = Guid.NewGuid(),
                LineUserId = "dev-admin-bbbbbb",
                DisplayName = "Demo Admin",
                SystemRole = SystemRole.Admin,
                CreatedAt = now.AddMinutes(-1),
                UpdatedAt = now,
            });
        await db.SaveChangesAsync();

        var users = await new AdminAppService(
            db,
            new ReviewAppService(db, TestDbFactory.NoopNotifications())).ListUsersAsync(CancellationToken.None);

        Assert.Equal(2, users.Count);
        Assert.Equal(users.Count, users.Select(u => u.Id).Distinct().Count());
        Assert.Contains(users, u => u.DisplayName == "Demo Admin" && u.LineUserId == "dev-admin-aaaaaa");
        Assert.Contains(users, u => u.DisplayName == "Demo Admin" && u.LineUserId == "dev-admin-bbbbbb");
    }
}
