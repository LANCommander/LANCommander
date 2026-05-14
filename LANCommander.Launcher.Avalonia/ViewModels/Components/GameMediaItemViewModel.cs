using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

/// <summary>Represents a single screenshot or video in the game detail media carousel.</summary>
public partial class GameMediaItemViewModel : ObservableObject
{
    /// <summary>Local file path or remote URL for videos.</summary>
    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private bool _isVideo;

    [ObservableProperty]
    private string _mimeType = string.Empty;

    /// <summary>Pre-loaded bitmap for screenshot display (loaded from local file or remote URL).</summary>
    [ObservableProperty]
    private Bitmap? _imageSource;

    /// <summary>When true, this item is a loading placeholder that will be replaced with real content.</summary>
    [ObservableProperty]
    private bool _isSkeleton;
}
