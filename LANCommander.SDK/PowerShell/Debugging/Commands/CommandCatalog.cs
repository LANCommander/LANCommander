#nullable enable
using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using PowerShellInstance = System.Management.Automation.PowerShell;

namespace LANCommander.SDK.PowerShell.Debugging.Commands;

/// <summary>
/// A long-lived runspace kept purely to answer "what commands exist?" while no script is being debugged.
/// </summary>
/// <remarks>
/// <para>
/// A debug run owns its runspace for the length of the run and tears it down afterwards, so for most of
/// the debugger's life there is no runspace to ask. This one exists so the command pane is populated
/// before the first run.
/// </para>
/// <para>
/// It deliberately does not pin the engine to one thread: queries arrive from the thread pool. A runspace
/// still runs one pipeline at a time, so calls are serialised behind a semaphore. It never executes user
/// script and is never debugged, and it is created lazily on the first query.
/// </para>
/// </remarks>
public sealed class CommandCatalog : IDisposable
{
    private readonly Func<Runspace> _openRunspace;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Runspace? _runspace;
    private bool _disposed;

    /// <param name="openRunspace">
    /// Opens the runspace to query. Should be built the same way script runspaces are, so the list
    /// includes the LANCommander cmdlets, plugin cmdlets and the modules scripts get.
    /// </param>
    public CommandCatalog(Func<Runspace> openRunspace) => _openRunspace = openRunspace;

    /// <summary>Every cmdlet, function and alias the session state can resolve.</summary>
    public Task<IReadOnlyList<CommandInfoSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
        QueryAsync(
            static invoke => CommandQuery.List(invoke),
            Array.Empty<CommandInfoSnapshot>(),
            cancellationToken);

    /// <summary>Synopsis, syntax and parameters for one command, or null if it cannot be resolved.</summary>
    public Task<CommandDetail?> DescribeAsync(string name, CancellationToken cancellationToken = default) =>
        QueryAsync<CommandDetail?>(
            invoke => CommandQuery.Describe(invoke, name),
            null,
            cancellationToken);

    private Task<TResult> QueryAsync<TResult>(
        Func<CommandQuery.Invoker, TResult> query,
        TResult fallback,
        CancellationToken cancellationToken) =>
        Task.Run(async () =>
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_disposed)
                    return fallback;

                var runspace = OpenIfNeeded();

                return query((command, output) =>
                {
                    using var powerShell = PowerShellInstance.Create();
                    powerShell.Runspace = runspace;
                    powerShell.Commands = command;
                    powerShell.Invoke(input: null, output: output);
                });
            }
            catch (Exception)
            {
                // The pane degrades to whatever the debug session can tell it. A failure to build a
                // catalogue is never worth taking the debugger down for.
                return fallback;
            }
            finally
            {
                _gate.Release();
            }
        }, cancellationToken);

    /// <summary>Called under <see cref="_gate"/>, so no additional locking is needed.</summary>
    private Runspace OpenIfNeeded()
    {
        if (_runspace is { RunspaceStateInfo.State: RunspaceState.Opened })
            return _runspace;

        _runspace?.Dispose();
        _runspace = _openRunspace();

        return _runspace;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Do not wait on the gate: a query in flight holds it, and Dispose runs on the UI thread during
        // window close. Closing the runspace under a running pipeline is safe and is what stops it.
        try { _runspace?.Dispose(); }
        catch (Exception) { }

        _runspace = null;
    }
}
