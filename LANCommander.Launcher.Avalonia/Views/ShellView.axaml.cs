using System;
using Avalonia.Controls;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class ShellView : UserControl
{
    private ChatWindow? _chatWindow;

    public ShellView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is ShellViewModel vm)
                vm.OpenChatRequested += OnOpenChatRequested;
        };
    }

    private void OnOpenChatRequested(object? sender, System.EventArgs e)
    {
        if (DataContext is not ShellViewModel vm)
            return;

        if (_chatWindow == null)
        {
            _chatWindow = new ChatWindow
            {
                DataContext = vm.Chat,
            };

            // Wire scroll-to-bottom when new messages arrive in the active thread
            vm.Chat.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ChatWindowViewModel.SelectedThread))
                    _chatWindow?.ScrollToBottom();
            };

            // When a message arrives while the window is inactive, activate it
            vm.Chat.MessageReceivedWhileInactive += (_, _) =>
            {
                if (!_chatWindow.IsVisible)
                    return; // only activate, don't show — the notification handles that
                Dispatcher.UIThread.InvokeAsync(_chatWindow.Activate);
            };
        }

        if (_chatWindow.IsVisible)
            _chatWindow.Activate();
        else
            _chatWindow.Show();
    }
}
