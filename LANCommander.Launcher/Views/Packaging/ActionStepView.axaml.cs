using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LANCommander.Launcher.Views.Packaging;

public partial class ActionStepView : UserControl
{
    public ActionStepView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Drops the executable list down as soon as the user lands in the box.
    /// </summary>
    private void OnExecutablePickerGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is AutoCompleteBox picker)
            Dispatcher.UIThread.Post(() => picker.IsDropDownOpen = true, DispatcherPriority.Background);
    }

    /// <summary>
    /// Opens the list from the caret button, for when the box already has focus and so will not
    /// raise <see cref="OnExecutablePickerGotFocus"/> again.
    /// </summary>
    private void OnBrowseExecutablesClick(object? sender, RoutedEventArgs e)
    {
        // The picker is the button's sibling in the path cell. Reached this way because names
        // inside a data template belong to each realised row, not to the view.
        if (sender is not Control button || button.Parent is not Panel cell)
            return;

        var picker = cell.Children.OfType<AutoCompleteBox>().FirstOrDefault();

        if (picker == null)
            return;

        picker.Focus();

        Dispatcher.UIThread.Post(() => picker.IsDropDownOpen = true, DispatcherPriority.Background);
    }
}
