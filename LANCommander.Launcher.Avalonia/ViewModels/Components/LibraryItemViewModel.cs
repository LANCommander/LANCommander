using System;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Data.Models;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

public partial class LibraryItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _hasIcon;

    [ObservableProperty]
    private bool _isSelected;

    public LibraryItemViewModel(Game game, string? iconPath = null)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        IconPath = iconPath;
        HasIcon = !string.IsNullOrEmpty(iconPath);
    }

    public LibraryItemViewModel(Guid id, string name)
    {
        Id = id;
        Title = name;
        HasIcon = false;
    }
}
