using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// Backs the unified "Manage" dialog. Hosts a list of sections (Options, Modify, Versions) shown in a
/// left-hand navigation menu; the selected section's <see cref="ManageSectionViewModel.Content"/> is
/// rendered in the right-hand pane.
/// </summary>
public partial class ManageOverlayViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ManageSectionViewModel> _sections = new();

    [ObservableProperty]
    private ManageSectionViewModel? _selectedSection;

    /// <summary>Raised when a section action asks the whole dialog to close (e.g. after queuing a reinstall).</summary>
    public event EventHandler? RequestClose;

    public void Close() => RequestClose?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A single navigable section within the Manage dialog. Wraps a content view-model and, optionally, a
/// footer action (Options → "Save", Modify → "Apply"). Sections that act per-row (Versions) leave
/// <see cref="ActionText"/> null and drive their own row commands instead.
/// </summary>
public partial class ManageSectionViewModel : ObservableObject
{
    public string Title { get; init; } = string.Empty;

    /// <summary>Phosphor icon name shown next to the section in the nav menu.</summary>
    public string IconValue { get; init; } = string.Empty;

    /// <summary>The content view-model rendered in the right pane.</summary>
    public object Content { get; init; } = null!;

    /// <summary>Footer action label, or null when the section has no single footer action.</summary>
    public string? ActionText { get; init; }

    public bool HasAction => !string.IsNullOrEmpty(ActionText);

    /// <summary>Work performed when the footer action button is pressed.</summary>
    public Func<Task>? ActionAsync { get; set; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _status;

    [RelayCommand]
    private async Task RunActionAsync()
    {
        if (IsBusy || ActionAsync is null)
            return;

        IsBusy = true;

        try
        {
            await ActionAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
