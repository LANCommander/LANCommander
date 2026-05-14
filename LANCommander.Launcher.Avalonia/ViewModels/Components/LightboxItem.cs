using Avalonia.Media.Imaging;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

public enum LightboxItemType
{
    Image,
    Video,
    Pdf
}

/// <summary>Represents a single item in the lightbox (screenshot, video, or PDF manual).</summary>
public class LightboxItem
{
    public LightboxItemType Type { get; set; }

    /// <summary>Local file path or streaming URL.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Optional title (used for PDFs).</summary>
    public string? Title { get; set; }

    /// <summary>Pre-loaded bitmap for screenshot display.</summary>
    public Bitmap? ImageSource { get; set; }
}
