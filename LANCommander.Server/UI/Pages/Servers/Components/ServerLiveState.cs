using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.UI.Pages.Servers.Components;

/// <summary>
/// What a server is doing right now, as ServerEditView samples it every two seconds for its header
/// and passes it on through a <see cref="ServerLiveFeed"/>, e.g. to the Monitor's process card.
/// </summary>
/// <param name="Status">Its process status.</param>
/// <param name="Process">The process sample; null when stopped or when the engine can't observe it.</param>
public sealed record ServerLiveState(ServerProcessStatus Status, ServerProcessInfo? Process)
{
    public static readonly ServerLiveState Unknown = new(ServerProcessStatus.Retrieving, null);

    /// <summary>How long the process has been up, e.g. "2h 41m"; null when not known.</summary>
    public string? Uptime(DateTime utcNow)
    {
        if (Process?.StartTime is not { } started)
            return null;

        var up = utcNow - (started.Kind == DateTimeKind.Local ? started.ToUniversalTime() : started);

        if (up < TimeSpan.Zero)
            up = TimeSpan.Zero;

        return up.TotalDays >= 1 ? $"{(int)up.TotalDays}d {up.Hours}h"
            : up.TotalHours >= 1 ? $"{(int)up.TotalHours}h {up.Minutes}m"
            : up.TotalMinutes >= 1 ? $"{up.Minutes}m"
            : $"{up.Seconds}s";
    }
}

/// <summary>
/// Carries a server's <see cref="ServerLiveState"/> from ServerEditView, which samples it, to the
/// parts of the page that show it (cascaded as "ServerLiveFeed"). Listeners re-render themselves,
/// so a CPU sample every two seconds doesn't re-render the whole section, e.g. a form being edited.
/// </summary>
public sealed class ServerLiveFeed
{
    public ServerLiveState State { get; private set; } = ServerLiveState.Unknown;

    /// <summary>Raised with the new state whenever it changes, on the sampling component's dispatcher.</summary>
    public event Action<ServerLiveState>? Changed;

    public void Publish(ServerLiveState state)
    {
        if (state == State)
            return;

        State = state;
        Changed?.Invoke(state);
    }
}
