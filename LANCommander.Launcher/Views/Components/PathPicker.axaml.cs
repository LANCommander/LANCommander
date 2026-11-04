using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LANCommander.SDK;

namespace LANCommander.Launcher.Views.Components;

public enum PathPickerMode
{
    Directory,
    File,
}

/// <summary>
/// A path text box with a Browse button. <see cref="Mode"/> decides whether the button opens a
/// folder picker or a file picker; the chosen path is written back to <see cref="Text"/>.
/// </summary>
public partial class PathPicker : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PathPicker, string?>(nameof(Text), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<PathPicker, string?>(nameof(Watermark));

    public static readonly StyledProperty<PathPickerMode> ModeProperty =
        AvaloniaProperty.Register<PathPicker, PathPickerMode>(nameof(Mode), PathPickerMode.Directory);

    public static readonly StyledProperty<string?> DialogTitleProperty =
        AvaloniaProperty.Register<PathPicker, string?>(nameof(DialogTitle));

    public static readonly StyledProperty<IReadOnlyList<FilePickerFileType>?> FileTypesProperty =
        AvaloniaProperty.Register<PathPicker, IReadOnlyList<FilePickerFileType>?>(nameof(FileTypes));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public PathPickerMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Title of the picker dialog. Defaults to "Choose a folder" / "Choose a file".</summary>
    public string? DialogTitle
    {
        get => GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    /// <summary>File type filters for <see cref="PathPickerMode.File"/>. Ignored for directories.</summary>
    public IReadOnlyList<FilePickerFileType>? FileTypes
    {
        get => GetValue(FileTypesProperty);
        set => SetValue(FileTypesProperty, value);
    }

    public PathPicker()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storageProvider == null)
            return;

        try
        {
            var startLocation = await GetStartLocationAsync(storageProvider);
            string? path;

            if (Mode == PathPickerMode.File)
            {
                var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = DialogTitle ?? "Choose a file",
                    AllowMultiple = false,
                    FileTypeFilter = FileTypes,
                    SuggestedStartLocation = startLocation,
                });

                path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            }
            else
            {
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = DialogTitle ?? "Choose a folder",
                    AllowMultiple = false,
                    SuggestedStartLocation = startLocation,
                });

                path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            }

            if (!string.IsNullOrWhiteSpace(path))
                Text = path;
        }
        catch
        {
            // A picker failure (no platform support, cancelled shell dialog) leaves the path as typed.
        }
    }

    /// <summary>
    /// Opens the picker at the current path, or the nearest folder above it that exists. Relative
    /// paths resolve beneath the config directory, the same way the SDK resolves them at runtime.
    /// </summary>
    private async Task<IStorageFolder?> GetStartLocationAsync(IStorageProvider storageProvider)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return null;

        try
        {
            var directory = AppPaths.ResolveStorageLocationPath(Text.Trim());

            if (Mode == PathPickerMode.File && !Directory.Exists(directory))
                directory = Path.GetDirectoryName(directory);

            while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                directory = Path.GetDirectoryName(directory);

            if (string.IsNullOrEmpty(directory))
                return null;

            return await storageProvider.TryGetFolderFromPathAsync(directory);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
