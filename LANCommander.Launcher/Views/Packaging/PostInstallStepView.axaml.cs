using Avalonia.Controls;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

public partial class PostInstallStepView : UserControl
{
    public PostInstallStepView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Picks a patch or mod installer and runs it under the same instrumentation the base
    /// install used, so whatever it writes joins the one change set.
    /// </summary>
    private async void OnRunInstallerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not PostInstallStepViewModel viewModel)
            return;

        await InstallerPicker.PickAndStartAsync(this, viewModel, "Choose a patch or mod installer to run");
    }
}
