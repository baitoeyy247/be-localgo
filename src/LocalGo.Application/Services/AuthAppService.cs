using LocalGo.Application.Abstractions.Auth;
using LocalGo.Application.Abstractions.Line;
using LocalGo.Application.Abstractions.Persistence;
using LocalGo.Application.Auth;
using LocalGo.Application.Common;
using LocalGo.Application.Dtos;
using LocalGo.Domain.Entities;
using LocalGo.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Application.Services;

public sealed class AuthAppService(
    ILocalGoDbContext db,
    IJwtTokenGenerator tokenGenerator,
    ILineIdTokenVerifier lineVerifier)
{
    public async Task<AuthResponse> LineLoginAsync(LineLoginRequest request, CancellationToken cancellationToken)
    {
        var profile = await lineVerifier.VerifyAsync(request.LineIdToken, cancellationToken);
        return await UpsertAndIssueTokenAsync(profile, cancellationToken);
    }

    public async Task<UserProfileDto?> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : MapUser(user);
    }

    public async Task<UserProfileDto> UpdateActiveRoleAsync(Guid userId, UpdateActiveRoleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AppRole>(request.ActiveRole, true, out var role))
        {
            throw new AppException("Invalid activeRole.", 400);
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AppException("User not found.", 404);

        user.ActiveRole = role;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapUser(user);
    }

    public async Task<UserProfileDto> AcceptPrivacyAsync(Guid userId, AcceptPrivacyRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new AppException("User not found.", 404);

        user.PrivacyNoticeAcceptedAt = DateTime.UtcNow;
        user.PrivacyNoticeVersion = request.Version;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapUser(user);
    }

    private async Task<AuthResponse> UpsertAndIssueTokenAsync(LineProfile profile, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == profile.LineUserId, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                LineUserId = profile.LineUserId,
                DisplayName = profile.DisplayName,
                PictureUrl = profile.PictureUrl,
                SystemRole = SystemRole.User,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
        }
        else
        {
            user.DisplayName = profile.DisplayName;
            user.PictureUrl = profile.PictureUrl;
            user.UpdatedAt = DateTime.UtcNow;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = tokenGenerator.GenerateToken(user),
            User = MapUser(user),
        };
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
