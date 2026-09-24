using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Views.Components;

/// <summary>
/// The title row shared by the overlay dialogs: a title, an optional subtitle and a close button.
/// The close button only raises <see cref="CloseRequested"/>; each dialog decides what closing means
/// (usually the same as Cancel).
/// </summary>
public partial class DialogTitleBar : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DialogTitleBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<DialogTitleBar, string?>(nameof(Subtitle));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public event EventHandler? CloseRequested;

    public DialogTitleBar()
    {
        InitializeComponent();
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
