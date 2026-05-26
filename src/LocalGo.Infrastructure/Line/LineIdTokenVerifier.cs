using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LocalGo.Application.Abstractions.Line;
using LocalGo.Application.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LocalGo.Infrastructure.Line;

public sealed class LineIdTokenVerifier(
    IHttpClientFactory httpClientFactory,
    IOptions<LineSettings> options,
    IHostEnvironment environment) : ILineIdTokenVerifier
{
    public async Task<LineProfile> VerifyAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ChannelId))
        {
            if (environment.IsDevelopment())
            {
                return new LineProfile($"dev-{Guid.NewGuid():N}", "LINE Dev User", null);
            }

            throw new AppException("LINE channel is not configured.", 503);
        }

        var client = httpClientFactory.CreateClient("line");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id_token"] = idToken,
            ["client_id"] = settings.ChannelId,
        });

        var response = await client.PostAsync("https://api.line.me/oauth2/v2.1/verify", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var normalizedError = NormalizeLineVerifyError(body);
            throw new AppException(normalizedError, 401);
        }

        var payload = await response.Content.ReadFromJsonAsync<LineVerifyResponse>(cancellationToken);
        if (payload?.Sub is null)
        {
            throw new AppException("Invalid LINE token payload.", 401);
        }

        return new LineProfile(payload.Sub, payload.Name ?? "LINE User", payload.Picture);
    }

    private sealed class LineVerifyResponse
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }

    private static string NormalizeLineVerifyError(string body)
    {
        var normalized = body.ToLowerInvariant();
        if (normalized.Contains("idtoken expired"))
        {
            return "Invalid LINE token: IdToken expired.";
        }

        return "Invalid LINE token.";
    }
}
