using Avalonia.Controls;
using Avalonia.Input;
using LANCommander.Launcher.ViewModels.ScriptDebugger;

namespace LANCommander.Launcher.Views.ScriptDebugger;

/// <summary>
/// Hosts the command browser. The only thing not expressible in markup is the double-click, which has no
/// command binding of its own.
/// </summary>
public partial class CommandsView : UserControl
{
    public CommandsView()
    {
        InitializeComponent();

        CommandList.DoubleTapped += OnListDoubleTapped;
    }

    private void OnListDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is CommandsViewModel model && model.InsertCommand.CanExecute(null))
            model.InsertCommand.Execute(null);
    }
}
