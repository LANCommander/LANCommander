namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

/// <summary>Represents a single screenshot or video in the game detail media carousel.</summary>
public class GameMediaItemViewModel
{
    public string Path       { get; set; } = string.Empty;
    public bool   IsVideo    { get; set; }
    public string MimeType   { get; set; } = string.Empty;
}
