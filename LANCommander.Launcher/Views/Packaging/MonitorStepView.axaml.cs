using Avalonia.Controls;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

public partial class MonitorStepView : UserControl
{
    public MonitorStepView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// File picking lives in the view: it needs the storage provider off the top-level window,
    /// which a view model has no business reaching for.
    /// </summary>
    private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MonitorStepViewModel viewModel)
            return;

    /// <remarks>
    /// Clipboard access hangs off the top level window, so this belongs in the view rather than
    /// the view model.
    /// </remarks>
        await InstallerPicker.PickAndStartAsync(this, viewModel, "Choose an installer to monitor");
    }
}
