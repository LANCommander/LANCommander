using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

public partial class GenreCarouselButtomViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _backgroundPath;

    [ObservableProperty]
    private bool _hasBackground;

    [ObservableProperty]
    private string? _name;

    public GenreCarouselButtomViewModel()
    {
    }

    public GenreCarouselButtomViewModel(SDK.Models.Genre genre, string? backgroundPath = null)
    {
        Name = genre.Name;
        
        BackgroundPath = backgroundPath;
        HasBackground = !string.IsNullOrEmpty(backgroundPath);
    }
}