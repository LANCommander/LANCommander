using System;
using System.Collections.Generic;
using Avalonia.Threading;
using LANCommander.Launcher.ViewModels.ScriptDebugger;
using LANCommander.Launcher.Views.ScriptDebugger;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Opens script debugger windows, one per game. Asking for a game whose window is already open brings
/// that window to the front instead of opening a second one, since two windows would compete for the
/// same scripts.
/// </summary>
public sealed class ScriptDebuggerWindowService(IServiceProvider services)
{
    private readonly Dictionary<Guid, ScriptDebuggerWindow> _windows = new();

    public void Open(Guid gameId) => Dispatcher.UIThread.Post(() => OpenOnUiThread(gameId));

    private void OpenOnUiThread(Guid gameId)
    {
        if (_windows.TryGetValue(gameId, out var existing))
        {
            existing.Activate();
            return;
        }

        var viewModel = new ScriptDebuggerWindowViewModel(services, gameId);
        var window = new ScriptDebuggerWindow { DataContext = viewModel };

        window.Closed += (_, _) =>
        {
            _windows.Remove(gameId);
            viewModel.Dispose();
        };

        _windows[gameId] = window;

        window.Show();

        _ = viewModel.LoadAsync();
    }
}
