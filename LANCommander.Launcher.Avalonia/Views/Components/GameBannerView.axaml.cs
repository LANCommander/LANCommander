using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class GameBannerView : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<GameBannerView, string?>(nameof(Title));

    public static readonly StyledProperty<string?> BannerPathProperty =
        AvaloniaProperty.Register<GameBannerView, string?>(nameof(BannerPath));

    public static readonly StyledProperty<string?> BackgroundPathProperty =
        AvaloniaProperty.Register<GameBannerView, string?>(nameof(BackgroundPath));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? BannerPath
    {
        get => GetValue(BannerPathProperty);
        set => SetValue(BannerPathProperty, value);
    }

    public string? BackgroundPath
    {
        get => GetValue(BackgroundPathProperty);
        set => SetValue(BackgroundPathProperty, value);
    }

    public GameBannerView()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            var fallbackTitle = this.FindControl<TextBlock>("FallbackTitle");
            if (fallbackTitle != null)
                fallbackTitle.Text = Title;
        }
        else if (change.Property == BannerPathProperty)
        {
            UpdateImages();
        }
        else if (change.Property == BackgroundPathProperty)
        {
            UpdateImages();
        }
    }

    private void UpdateImages()
    {
        var bannerImage = this.FindControl<Image>("BannerImage");
        var backgroundImage = this.FindControl<Image>("BackgroundImage");
        var fallbackBanner = this.FindControl<Border>("FallbackBanner");

        if (bannerImage != null)
        {
            if (!string.IsNullOrEmpty(BannerPath))
            {
                try
                {
                    bannerImage.Source = new Bitmap(BannerPath);
                    bannerImage.IsVisible = true;
                    if (fallbackBanner != null)
                        fallbackBanner.IsVisible = false;
                }
                catch
                {
                    bannerImage.IsVisible = false;
                    if (fallbackBanner != null)
                        fallbackBanner.IsVisible = true;
                }
            }
            else
            {
                bannerImage.IsVisible = false;
                if (fallbackBanner != null)
                    fallbackBanner.IsVisible = true;
            }
        }

        if (backgroundImage != null && !string.IsNullOrEmpty(BackgroundPath))
        {
            try
            {
                backgroundImage.Source = new Bitmap(BackgroundPath);
                backgroundImage.IsVisible = true;
            }
            catch
            {
                backgroundImage.IsVisible = false;
            }
        }
    }
}
