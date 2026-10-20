using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Commands;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>Where a command list came from. Shown in the pane, because the two differ.</summary>
public enum CommandSource
{
    /// <summary>The standalone catalogue runspace: what every script starts with.</summary>
    Catalog,

    /// <summary>The stopped debug session: also includes whatever the script has imported.</summary>
    Session,
}

/// <summary>
/// Picks which runspace answers a command query.
/// </summary>
/// <remarks>
/// The debug runspace is the better source, since it is the one the script actually runs in, but it
/// exists only during a run and is only reachable while stopped. The catalogue covers the rest of the
/// time, and both paths return the same DTOs.
/// </remarks>
public sealed class CommandCatalogService : IDisposable
{
    private readonly DebugSessionController _controller;
    private readonly CommandCatalog _catalog;

    public CommandCatalogService(DebugSessionController controller, CommandCatalog catalog)
    {
        _controller = controller;
        _catalog = catalog;
    }

    /// <summary>True while the live session can be queried, i.e. while the debugger is stopped.</summary>
    public bool CanQuerySession =>
        _controller.State is DebugSessionState.Stopped or DebugSessionState.Evaluating;

    public async Task<(IReadOnlyList<CommandInfoSnapshot> Commands, CommandSource Source)> ListAsync()
    {
        if (CanQuerySession)
        {
            var live = await _controller.GetCommandsAsync().ConfigureAwait(true);

            // An empty result means the query timed out or the script resumed underneath it; the
            // catalogue is a better answer than an empty pane.
            if (live.Count > 0)
                return (live, CommandSource.Session);
        }

        return (await _catalog.ListAsync().ConfigureAwait(true), CommandSource.Catalog);
    }

    /// <summary>
    /// Details for one command, from the live session when there is one, so a command that only exists
    /// because the script imported its module can still be described.
    /// </summary>
    public async Task<CommandDetail?> DescribeAsync(string name)
    {
        if (CanQuerySession)
        {
            var live = await _controller.GetCommandDetailAsync(name).ConfigureAwait(true);

            if (live is not null)
                return live;
        }

        return await _catalog.DescribeAsync(name).ConfigureAwait(true);
    }

    public void Dispose() => _catalog.Dispose();
}
