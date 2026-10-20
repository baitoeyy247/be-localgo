using LocalGo.Application.Services;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;

namespace LocalGo.Tests;

public sealed class AdminUserListTests
{
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

        var users = await new AdminAppService(db).ListUsersAsync(CancellationToken.None);

        Assert.Equal(2, users.Count);
        Assert.Equal(users.Count, users.Select(u => u.Id).Distinct().Count());
        Assert.Contains(users, u => u.DisplayName == "Demo Admin" && u.LineUserId == "dev-admin-aaaaaa");
        Assert.Contains(users, u => u.DisplayName == "Demo Admin" && u.LineUserId == "dev-admin-bbbbbb");
    }
}
