namespace LocalGo.Infrastructure.Notifications;

public sealed class NotifySettings
{
    public const string SectionName = "Notify";

    /// <summary>Delivery channel: <c>line</c> (default) or <c>console</c> (structured stdout, no LINE API).</summary>
    public string Mode { get; set; } = "line";

    public bool IsConsole =>
        string.Equals(Mode, "console", StringComparison.OrdinalIgnoreCase);

    public bool IsLine =>
        string.IsNullOrWhiteSpace(Mode)
        || string.Equals(Mode, "line", StringComparison.OrdinalIgnoreCase);
}
