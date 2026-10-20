using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>Represents a single screenshot or video in the game detail media carousel.</summary>
public partial class GameMediaItemViewModel : ObservableObject
{
    /// <summary>Local file path or remote URL for videos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoPath))]
    private string _path = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoPath))]
    private bool _isVideo;

    /// <summary>
    /// Path for the inline video player — only populated for video items. The carousel
    /// template instantiates an InlineVideoPlayer for every item (even hidden ones for
    /// screenshots), so binding the screenshot path here would make libvlc try to "play"
    /// the image and leak a decoder per item. Null for screenshots keeps the player idle.
    /// </summary>
    public string? VideoPath => IsVideo ? Path : null;

    [ObservableProperty]
    private string _mimeType = string.Empty;

    /// <summary>Name given to the media on the server; the lightbox shows it as a caption.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Pre-loaded bitmap for screenshot display (loaded from local file or remote URL).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AspectRatio))]
    private Bitmap? _imageSource;

    /// <summary>Width over height for layout: the screenshot's own shape, 16:9 for videos and placeholders.</summary>
    public double AspectRatio =>
        ImageSource is { Size: { Width: > 0, Height: > 0 } size } ? size.Width / size.Height : 16.0 / 9.0;

    /// <summary>When true, this item is a loading placeholder that will be replaced with real content.</summary>
    [ObservableProperty]
    private bool _isSkeleton;
}
