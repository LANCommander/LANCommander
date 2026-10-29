using System.Collections.Concurrent;
using System.Data.Common;
using LANCommander.Server.Plugins.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// Each plugin context's state. Nothing hands a context out, or lets one connect, unless its state
/// is <see cref="PluginDataStatus.Ready"/>.
/// </summary>
public sealed class PluginDataGate
{
    private readonly ConcurrentDictionary<Type, PluginDataState> _states = new();

    /// <summary>Raised after a context's state changes.</summary>
    public event Action<Type, PluginDataState>? Changed;

    public void Set(Type contextType, PluginDataState state)
    {
        _states[contextType] = state;

        Changed?.Invoke(contextType, state);
    }

    public PluginDataState Get(Type contextType) => _states.GetValueOrDefault(contextType, PluginDataState.Unvalidated);

    /// <exception cref="PluginDataUnavailableException">The context isn't ready.</exception>
    public void EnsureReady(Type contextType)
    {
        var state = Get(contextType);

        if (state.Status != PluginDataStatus.Ready)
            throw new PluginDataUnavailableException(contextType, state);
    }
}

/// <summary>
/// The gate's backstop: a context created while its plugin was ready, and kept after it stopped
/// being, such as one held by a long-running job, can't open a connection.
/// </summary>
public sealed class PluginDataConnectionInterceptor(PluginDataGate gate) : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        gate.EnsureReady(eventData.Context!.GetType());

        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        gate.EnsureReady(eventData.Context!.GetType());

        return ValueTask.FromResult(result);
    }
}

/// <summary>A plugin's <c>IDbContextFactory</c>, which checks the gate before creating a context.</summary>
internal sealed class GatedContextFactory<TContext>(IServiceProvider services, PluginDataGate gate, PluginContextOptionsFactory options)
    : IDbContextFactory<TContext>
    where TContext : PluginDbContext
{
    public TContext CreateDbContext()
    {
        gate.EnsureReady(typeof(TContext));

        return ActivatorUtilities.CreateInstance<TContext>(services, options.CreateForActiveEngine(typeof(TContext)));
    }
}
