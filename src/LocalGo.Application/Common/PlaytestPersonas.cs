using LocalGo.Domain.Enums;

namespace LocalGo.Application.Common;

/// <summary>Canonical SIT playtest persona labels (entry list, user display name, provider name).</summary>
public static class PlaytestPersonas
{
    public static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["req-01"] = "QA ผู้รับ 01",
            ["req-02"] = "QA ผู้รับ 02",
            ["req-03"] = "QA ผู้รับ 03",
            ["req-04"] = "QA ผู้รับ 04",
            ["prov-01"] = "QA ผู้ให้ 01",
            ["prov-02"] = "QA ผู้ให้ 02",
            ["prov-03"] = "QA ผู้ให้ 03",
            ["both-01"] = "QA สองบทบาท 01",
            ["both-02"] = "QA สองบทบาท 02",
            ["admin-01"] = "QA แอดมิน",
        };

    public static bool TryGetDisplayName(string? personaKey, out string displayName)
    {
        displayName = string.Empty;
        return personaKey is not null && DisplayNames.TryGetValue(personaKey, out displayName);
    }

    public static bool UsesProviderProfileName(string personaKey) =>
        personaKey is "prov-01" or "prov-02" or "prov-03" or "both-02";

    public static bool IsDualPersonaLineId(string lineUserId) =>
        lineUserId.Contains("both-", StringComparison.Ordinal);

    public static bool CanActAsRequester(string lineUserId, bool hasProviderProfile, AppRole? activeRole)
    {
        if (IsDualPersonaLineId(lineUserId))
        {
            return true;
        }

        if (!hasProviderProfile)
        {
            return true;
        }

        return activeRole is null or AppRole.Requester;
    }
}
