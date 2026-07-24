using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LANCommander.Launcher.Plugins.Contributions;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Views;

public partial class ShellView : UserControl
{
    private ChatWindow? _chatWindow;
    private ShellViewModel? _previousVm;
    private System.ComponentModel.PropertyChangedEventHandler? _chatPropertyChangedHandler;
    private EventHandler<(string threadTitle, string senderName)>? _chatMessageReceivedHandler;

    public ShellView()
    {
        InitializeComponent();

        // Content templates come from the shared view registry (seeded with the built-in mappings and
        // extendable by plugins) rather than inline XAML DataTemplates.
        var registry = App.Services?.GetService<LANCommander.Launcher.Plugins.IViewRegistry>();
        if (registry != null)
            ContentHost.DataTemplates.Add(registry.AsDataTemplate());

        AppendFooterContributions();

        KeyDown += OnKeyDown;

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

    /// <summary>
    /// Renders any plugin-contributed footer controls (via <see cref="IFooterContribution"/>) to the
    /// left of the chat button, ordered by their declared <c>Order</c>. A failing contribution is
    /// skipped so the built-in footer still renders.
    /// </summary>
    private void AppendFooterContributions()
    {
        var contributions = App.Services?
            .GetServices<IFooterContribution>()
            .OrderBy(c => c.Order)
            .ToList();

        if (contributions == null || contributions.Count == 0)
            return;

        foreach (var contribution in contributions)
        {
            try
            {
                FooterPluginItems.Children.Add(contribution.BuildContent());
            }
            catch
            {
                // A misbehaving plugin must not break the shell footer.
            }
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !e.Handled)
        {
            var nav = App.Services.GetService<INavigationService>();
            if (nav is { CanGoBack: true })
            {
                nav.GoBack();
                e.Handled = true;
            }
        }
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
