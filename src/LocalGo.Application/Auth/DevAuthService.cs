using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Auth;

public sealed class DevAuthService(
    ILocalGoDbContext db,
    IJwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResponse> LoginAsync(DevLoginRequest request, CancellationToken cancellationToken)
    {
        var lineUserId = $"dev-{Guid.NewGuid():N}";
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? "Dev User"
            : request.DisplayName.Trim();

        AppRole? activeRole = null;
        if (!string.IsNullOrWhiteSpace(request.ActiveRole) &&
            Enum.TryParse<AppRole>(request.ActiveRole, true, out var parsed))
        {
            activeRole = parsed;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            LineUserId = lineUserId,
            DisplayName = displayName,
            SystemRole = request.AsAdmin ? SystemRole.Admin : SystemRole.User,
            ActiveRole = activeRole,
            Status = UserStatus.Active,
            PrivacyNoticeAcceptedAt = DateTime.UtcNow,
            PrivacyNoticeVersion = "dev",
            LastLoginAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = tokenGenerator.GenerateToken(user),
            User = MapUser(user),
        };
    }

    public async Task<UserProfileDto?> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is null ? null : MapUser(user);
    }

    private static UserProfileDto MapUser(User user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        PictureUrl = user.PictureUrl,
        SystemRole = user.SystemRole.ToString(),
        ActiveRole = user.ActiveRole?.ToString(),
        PrivacyNoticeAccepted = user.PrivacyNoticeAcceptedAt.HasValue,
    };
}
