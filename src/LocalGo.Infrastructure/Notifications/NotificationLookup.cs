using System.Text.Json;
using LocalGo.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LocalGo.Infrastructure.Notifications;

public sealed record NotificationRequestInfo(string? Title, string? CategoryName, Guid RequesterUserId);

/// <summary>
/// A-3: page-level batch of the rows <see cref="NotificationMessageBuilder"/> needs, so rendering
/// N notification previews costs a fixed number of queries (≤ 2) instead of up to 3 per row.
/// </summary>
public sealed class NotificationLookup
{
    private static readonly string[] RequestKeys = ["requestId", "serviceRequestId"];

    public required IReadOnlyDictionary<Guid, NotificationRequestInfo> Requests { get; init; }
    public required IReadOnlyDictionary<Guid, string> Categories { get; init; }

    public static async Task<NotificationLookup> LoadAsync(
        IEnumerable<JsonElement?> payloads,
        ILocalGoDbContext db,
        CancellationToken cancellationToken)
    {
        var requestIds = new HashSet<Guid>();
        var categoryIds = new HashSet<Guid>();
        foreach (var p in payloads)
        {
            if (p is not { ValueKind: JsonValueKind.Object } obj) continue;
            foreach (var key in RequestKeys)
            {
                if (TryGuid(obj, key, out var id)) requestIds.Add(id);
            }

            if (TryGuid(obj, "categoryId", out var cat)) categoryIds.Add(cat);
        }

        var requests = requestIds.Count == 0
            ? new Dictionary<Guid, NotificationRequestInfo>()
            : await db.ServiceRequests.AsNoTracking()
                .Where(r => requestIds.Contains(r.Id))
                .Select(r => new { r.Id, Info = new NotificationRequestInfo(r.Title, r.Category.Name, r.RequesterUserId) })
                .ToDictionaryAsync(x => x.Id, x => x.Info, cancellationToken);

        var categories = categoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.ServiceCategories.AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        return new NotificationLookup { Requests = requests, Categories = categories };
    }

    private static bool TryGuid(JsonElement obj, string key, out Guid id)
    {
        id = Guid.Empty;
        return obj.TryGetProperty(key, out var v)
            && v.ValueKind == JsonValueKind.String
            && Guid.TryParse(v.GetString(), out id);
    }
}
