using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class PageHeaderView : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageHeaderView, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PageHeaderView, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string?> BackButtonTextProperty =
        AvaloniaProperty.Register<PageHeaderView, string?>(nameof(BackButtonText));

    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<PageHeaderView, ICommand?>(nameof(BackCommand));

    public static readonly StyledProperty<bool> ShowBackButtonProperty =
        AvaloniaProperty.Register<PageHeaderView, bool>(nameof(ShowBackButton));

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

    public string? BackButtonText
    {
        get => GetValue(BackButtonTextProperty);
        set => SetValue(BackButtonTextProperty, value);
    }

    public ICommand? BackCommand
    {
        get => GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public bool ShowBackButton
    {
        get => GetValue(ShowBackButtonProperty);
        set => SetValue(ShowBackButtonProperty, value);
    }

    public PageHeaderView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            var titleText = this.FindControl<TextBlock>("TitleText");
            if (titleText != null)
                titleText.Text = Title;
        }
        else if (change.Property == SubtitleProperty)
        {
            var subtitleText = this.FindControl<TextBlock>("SubtitleText");
            if (subtitleText != null)
            {
                subtitleText.Text = Subtitle;
                subtitleText.IsVisible = !string.IsNullOrEmpty(Subtitle);
            }
        }
        else if (change.Property == BackButtonTextProperty || change.Property == BackCommandProperty || change.Property == ShowBackButtonProperty)
        {
            var backButton = this.FindControl<Button>("BackButton");
            if (backButton != null)
            {
                backButton.Content = BackButtonText ?? "← Back";
                backButton.Command = BackCommand;
                backButton.IsVisible = ShowBackButton;
            }
        }
    }
}
