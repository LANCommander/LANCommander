using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Plugins.Extensions;
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

        AppendFooterExtensions();

        KeyDown += OnKeyDown;

        // The title list consumes arrow keys itself, so Right out of it needs handled events too
        AddHandler(KeyDownEvent, OnRailKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);

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
    /// Renders any plugin footer controls (via <see cref="IFooterExtension"/>) to the
    /// left of the chat button, ordered by their declared <c>Order</c>. A failing extension is
    /// skipped so the built-in footer still renders.
    /// </summary>
    private void AppendFooterExtensions()
    {
        var extensions = App.Services?
            .GetServices<IFooterExtension>()
            .OrderBy(c => c.Order)
            .ToList();

        if (extensions == null || extensions.Count == 0)
            return;

        foreach (var extension in extensions)
        {
            try
            {
                FooterPluginItems.Children.Add(extension.BuildContent());
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

    /// <summary>
    /// Moves focus between the library rail's title list and the content beside it: Right out of the
    /// list, and Left out of the content when nothing in it took the key.
    /// </summary>
    private void OnRailKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel { IsLibraryRail: true })
            return;

        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
            return;

        if (e.Key == Key.Right && LibraryRail.ContainsInList(focused))
        {
            if (FocusContent())
                e.Handled = true;
        }
        else if (e.Key == Key.Left && !e.Handled
                 && ContentHost.IsVisualAncestorOf(focused)
                 && focused is not TextBox)
        {
            LibraryRail.FocusSelectedTitle();
            e.Handled = true;
        }
    }

    private bool FocusContent()
    {
        // The library's list layout knows its sections; anything else gets its top-left focusable
        // control. Not the first in tree order: a page can declare a bottom bar first, as the
        // library's filter bar is.
        var libraryRow = ContentHost.GetVisualDescendants()
            .OfType<LibraryRowView>()
            .FirstOrDefault(v => v.IsEffectivelyVisible);

        if (libraryRow != null)
        {
            libraryRow.FocusFirstSection();
            return true;
        }

        var target = ContentHost.GetVisualDescendants()
            .OfType<InputElement>()
            .Where(el => el.Focusable && el.IsEffectivelyVisible && el.IsEffectivelyEnabled
                && el is not ItemsControl && el is not ScrollViewer && el is not TextBox)
            .Select(el => (Element: el, Position: el.TranslatePoint(default, ContentHost)))
            .Where(c => c.Position is { } p && p.Y >= 0 && p.Y < ContentHost.Bounds.Height)
            .OrderBy(c => Math.Round(c.Position!.Value.Y))
            .ThenBy(c => c.Position!.Value.X)
            .Select(c => c.Element)
            .FirstOrDefault();

        return target?.Focus(NavigationMethod.Directional) == true;
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
