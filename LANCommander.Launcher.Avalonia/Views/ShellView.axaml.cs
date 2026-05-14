using System;
using Avalonia.Controls;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class ShellView : UserControl
{
    private ChatWindow? _chatWindow;
    private ShellViewModel? _previousVm;
    private System.ComponentModel.PropertyChangedEventHandler? _chatPropertyChangedHandler;
    private EventHandler<(string threadTitle, string senderName)>? _chatMessageReceivedHandler;

    public ShellView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_previousVm != null)
                _previousVm.OpenChatRequested -= OnOpenChatRequested;

            if (DataContext is ShellViewModel vm)
            {
                vm.OpenChatRequested += OnOpenChatRequested;
                _previousVm = vm;
            }
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
            _chatPropertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(ChatWindowViewModel.SelectedThread))
                    _chatWindow?.ScrollToBottom();
            };
            vm.Chat.PropertyChanged += _chatPropertyChangedHandler;

            // When a message arrives while the window is inactive, activate it
            _chatMessageReceivedHandler = (_, _) =>
            {
                if (!_chatWindow.IsVisible)
                    return; // only activate, don't show — the notification handles that
                Dispatcher.UIThread.InvokeAsync(_chatWindow.Activate);
            };
            vm.Chat.MessageReceivedWhileInactive += _chatMessageReceivedHandler;

            _chatWindow.Closed += (_, _) =>
            {
                if (_chatPropertyChangedHandler != null)
                    vm.Chat.PropertyChanged -= _chatPropertyChangedHandler;
                if (_chatMessageReceivedHandler != null)
                    vm.Chat.MessageReceivedWhileInactive -= _chatMessageReceivedHandler;
                _chatPropertyChangedHandler = null;
                _chatMessageReceivedHandler = null;
                _chatWindow = null;
            };
        }

        if (_chatWindow.IsVisible)
            _chatWindow.Activate();
        else
            _chatWindow.Show();
    }
}
