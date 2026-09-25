#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// The in-process broker: a registry of open debugger UIs. A script attaches to the first registered
/// target that matches it.
/// </summary>
public sealed class ScriptDebugBroker : IScriptDebugBroker
{
    private readonly object _gate = new();
    private IScriptDebugTarget[] _targets = Array.Empty<IScriptDebugTarget>();

    /// <summary>Register a target. Dispose the result to unregister it.</summary>
    public IDisposable Register(IScriptDebugTarget target)
    {
        lock (_gate)
            _targets = _targets.Append(target).ToArray();

        return new Registration(this, target);
    }

    public IReadOnlyList<IScriptDebugTarget> Targets => Volatile.Read(ref _targets);

    public bool IsAttached(ScriptIdentity identity) => Find(identity) is not null;

    public void PrepareScriptFile(ScriptIdentity identity, string path) =>
        Find(identity)?.PrepareScriptFile(identity, path);

    public ValueTask<ScriptDebugAttachment?> TryAttachAsync(ScriptIdentity identity, CancellationToken cancellationToken = default) =>
        new(Find(identity)?.BeginAttach(identity));

    public ScriptDebugRemoteEndpoint? GetRemoteEndpoint(ScriptIdentity identity) =>
        Find(identity)?.RemoteEndpoint;

    private IScriptDebugTarget? Find(ScriptIdentity identity)
    {
        foreach (var target in Volatile.Read(ref _targets))
        {
            try
            {
                if (target.Matches(identity))
                    return target;
            }
            catch (Exception)
            {
                // A misbehaving target must never stop a script from running.
            }
        }

        return null;
    }

    private void Unregister(IScriptDebugTarget target)
    {
        lock (_gate)
            _targets = _targets.Where(t => !ReferenceEquals(t, target)).ToArray();
    }

    private sealed class Registration(ScriptDebugBroker broker, IScriptDebugTarget target) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                broker.Unregister(target);
        }
    }
}
