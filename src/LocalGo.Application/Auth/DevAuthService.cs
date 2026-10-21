using System.Text.RegularExpressions;
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
    private static readonly Regex PersonaKeyPattern = new(
        "^[a-z0-9][a-z0-9-]{0,31}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Personas that should start with a provider profile shell (M6).</summary>
    private static readonly HashSet<string> SeedProviderPersonas = new(StringComparer.Ordinal)
    {
        "prov-01",
        "prov-02",
        "prov-03",
        "both-02",
    };

    public async Task<AuthResponse> LoginAsync(DevLoginRequest request, CancellationToken cancellationToken)
    {
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? "Dev User"
            : request.DisplayName.Trim();

        AppRole? activeRole = null;
        if (!string.IsNullOrWhiteSpace(request.ActiveRole) &&
            Enum.TryParse<AppRole>(request.ActiveRole, true, out var parsed))
        {
            activeRole = parsed;
        }

        var personaKey = NormalizePersonaKey(request.PersonaKey);
        var lineUserId = personaKey is null
            ? $"dev-{Guid.NewGuid():N}"
            : $"dev-persona-{personaKey}";

        User? user = null;
        if (personaKey is not null)
        {
            user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == lineUserId, cancellationToken);
        }

        if (user is null)
        {
            user = new User
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
        }
        else
        {
            user.DisplayName = displayName;
            user.SystemRole = request.AsAdmin ? SystemRole.Admin : SystemRole.User;
            if (activeRole.HasValue)
            {
                user.ActiveRole = activeRole;
            }

            user.Status = UserStatus.Active;
            user.PrivacyNoticeAcceptedAt ??= DateTime.UtcNow;
            user.PrivacyNoticeVersion ??= "dev";
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (personaKey is not null && SeedProviderPersonas.Contains(personaKey))
        {
            await EnsureProviderProfileAsync(user, cancellationToken);
        }

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

    private async Task EnsureProviderProfileAsync(User user, CancellationToken cancellationToken)
    {
        var exists = await db.Providers.AnyAsync(p => p.OwnerUserId == user.Id, cancellationToken);
        if (exists)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Providers.Add(new Provider
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            ProviderType = ProviderType.Individual,
            Name = user.DisplayName,
            Description = "โปรไฟล์ทดสอบ — สลับบทบาทได้โดยไม่ลบข้อมูลเดิม",
            Status = ProviderStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizePersonaKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var key = raw.Trim().ToLowerInvariant();
        return PersonaKeyPattern.IsMatch(key) ? key : null;
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
