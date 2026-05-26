namespace LocalGo.Infrastructure.Line;

public sealed class LineSettings
{
    public const string SectionName = "Line";

    public string ChannelId { get; set; } = string.Empty;
    public string ChannelSecret { get; set; } = string.Empty;
    public string LiffId { get; set; } = string.Empty;
    public string MessagingAccessToken { get; set; } = string.Empty;

    /// <summary>Base URL for LIFF deep links, e.g. https://liff.line.me/{LiffId}</summary>
    public string LiffBaseUrl => string.IsNullOrWhiteSpace(LiffId)
        ? string.Empty
        : $"https://liff.line.me/{LiffId}";

    public bool MessagingReady =>
        !string.IsNullOrWhiteSpace(MessagingAccessToken)
        && !MessagingAccessToken.StartsWith("YOUR_", StringComparison.Ordinal);
}
