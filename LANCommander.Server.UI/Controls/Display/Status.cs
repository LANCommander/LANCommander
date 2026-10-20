namespace LANCommander.Server.UI.Controls;

/// <summary>
/// The semantic colour a control is drawn in. Controls expose it as bool parameters
/// (<c>Success</c>, <c>Info</c>, <c>Warning</c>, <c>Danger</c>); this resolves them to one value.
/// </summary>
internal enum Status
{
    None,
    Primary,
    Success,
    Info,
    Warning,
    Danger,
}

internal static class StatusResolver
{
    /// <summary>When several flags are set the most severe wins: Danger, Warning, Success, Info, Primary.</summary>
    public static Status Resolve(bool primary = false, bool success = false, bool info = false, bool warning = false, bool danger = false) =>
        danger ? Status.Danger
        : warning ? Status.Warning
        : success ? Status.Success
        : info ? Status.Info
        : primary ? Status.Primary
        : Status.None;

    public static string? ClassSuffix(this Status status) => status switch
    {
        Status.Primary => "primary",
        Status.Success => "success",
        Status.Info => "info",
        Status.Warning => "warning",
        Status.Danger => "danger",
        _ => null,
    };

    /// <summary>The icon conventionally shown for a status.</summary>
    public static IconType Icon(this Status status) => status switch
    {
        Status.Success => IconType.CheckCircle,
        Status.Warning => IconType.Warning,
        Status.Danger => IconType.XCircle,
        _ => IconType.Info,
    };
}
