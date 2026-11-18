using System.Security.Cryptography;
using System.Text;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Lists game servers that live somewhere other than the server's database, e.g. those another system
/// already manages, so they appear on the Servers page beside the ones administrators add. Register an
/// implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike an <see cref="IServerEngineExtension"/>, which runs servers the administrator adds and the
/// server stores, a source decides which servers there are. Nothing about them is stored: the server
/// asks for the list every <see cref="RefreshInterval"/>, keeps the latest in memory, and shows each
/// server's name, game, address and status from it. A server that is no longer listed disappears.
/// </para>
/// <para>
/// Each server is known to the rest of the server by an id worked out from the source's
/// <see cref="Id"/> and the server's <see cref="ExternalServer.Key"/>; see
/// <see cref="ExternalServerIds.Create"/>. It is the <c>ServerId</c> of the
/// <c>ServerStatusChangedEvent</c>s raised for it, and stays the same while the key does.
/// </para>
/// <para>
/// Nothing about a listed server is configured here: its game is the one the source reports, and it
/// doesn't start on its own. A plugin whose servers should be configured like the server's own, with
/// autostart, actions and scripts, adds them with <see cref="IServerProvider"/> instead, run by its
/// <see cref="IServerEngineExtension"/>.
/// </para>
/// <para>
/// The source is a singleton and may be called concurrently.
/// </para>
/// </remarks>
public interface IServerSourceExtension
{
    /// <summary>
    /// Identifies the source. Part of every one of its servers' ids, so it must never change, e.g.
    /// <c>com.mycompany.myplugin.servers</c>.
    /// </summary>
    string Id { get; }

    /// <summary>The source's name, shown beside its servers on the Servers page.</summary>
    string Name { get; }

    /// <summary>
    /// How often <see cref="GetServersAsync"/> is called. Ask for a refresh sooner with
    /// <see cref="IServerSourceHost.Refresh"/>. At least a second.
    /// </summary>
    TimeSpan RefreshInterval => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Called once, when the server starts, before any other call. <paramref name="host"/> is how the
    /// source reports changes between refreshes; keep it.
    /// </summary>
    Task InitializeAsync(IServerSourceHost host, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Lists the source's servers as they are now, status included. Called every
    /// <see cref="RefreshInterval"/>, so it should be reasonably cheap. Throw when the list can't be
    /// read, e.g. a host is unreachable: the servers last listed are kept, marked as errored, until a
    /// list is read again. Servers with no key, or one another server already has, are left out.
    /// </summary>
    Task<IReadOnlyList<ExternalServer>> GetServersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts a server whose <see cref="ExternalServer.CanStart"/> is set, returning once it has been
    /// asked to rather than when it stops. Throw to report that it could not be started; the server
    /// shows the message. The list is refreshed soon after.
    /// </summary>
    Task StartAsync(ExternalServer server, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{Name} can't start its servers.");

    /// <summary>
    /// Stops a server whose <see cref="ExternalServer.CanStop"/> is set. Throw to report that it could
    /// not be stopped. The list is refreshed soon after.
    /// </summary>
    Task StopAsync(ExternalServer server, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{Name} can't stop its servers.");

    /// <summary>
    /// Pages of the source's own for a server, listed under the source's name in the server's menu
    /// beside its General and Monitor pages, e.g. its players or its configuration on the system it
    /// comes from. Called whenever the server's pages are drawn, so it should be cheap and shouldn't
    /// reach out to anything; work it out from the server as listed. Register the components' assembly
    /// with <c>IServerRouteAssemblyExtension</c> only if they are also pages of their own.
    /// </summary>
    IReadOnlyList<ExternalServerSection> GetSections(ExternalServer server) => [];

    /// <summary>
    /// A Razor component of the source's own shown at the top of a server's General page, above what
    /// the server shows of every listed server, e.g. a summary from the system it comes from. It takes
    /// the <see cref="ExternalServer"/> as its <c>Server</c> parameter, refreshed as the source lists it
    /// again, and the id the server knows it by as its <c>ServerId</c> parameter. Called whenever the
    /// page is drawn, so it should be cheap. Null shows nothing.
    /// </summary>
    Type? GetOverview(ExternalServer server) => null;

    /// <summary>
    /// Reads the output of a server with <see cref="ExternalServerFeatures.Log"/>, oldest line first,
    /// for its Monitor page: the latest <see cref="ServerEngineLogRequest.Tail"/> lines, then, when
    /// <see cref="ServerEngineLogRequest.Follow"/> is set, lines as they are written until the reader
    /// cancels. Throw to report that it can't be read; the page shows the message.
    /// </summary>
    IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ExternalServer server, ServerEngineLogRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{Name} can't read its servers' output.");

    /// <summary>
    /// Samples a running server with <see cref="ExternalServerFeatures.ProcessInfo"/>, every couple of
    /// seconds while its Monitor page is open. Null when it can't be sampled.
    /// </summary>
    Task<ServerEngineProcessInfo?> GetProcessInfoAsync(ExternalServer server, CancellationToken cancellationToken)
        => Task.FromResult<ServerEngineProcessInfo?>(null);

    /// <summary>
    /// Sends a command to a running server with <see cref="ExternalServerFeatures.Commands"/>, from its
    /// Monitor page, e.g. a line to its console. Returns what the server answered where it answers
    /// directly (as over RCON), or null where its answer appears in its output. Throw to report that
    /// the command couldn't be sent; the page shows the message.
    /// </summary>
    Task<string?> SendCommandAsync(ExternalServer server, string command, CancellationToken cancellationToken)
        => throw new NotSupportedException($"{Name} can't send commands to its servers.");
}

/// <summary>A page an <see cref="IServerSourceExtension"/> adds to one of its servers.</summary>
/// <param name="Slug">
/// The page's address under the server, <c>/Servers/External/{id}/Source/{Slug}</c>: letters, digits
/// and <c>-</c>, unique among the server's pages.
/// </param>
/// <param name="Title">The page's name in the menu and its title.</param>
/// <param name="Component">
/// A Razor component rendered as the page, taking the <see cref="ExternalServer"/> as its <c>Server</c>
/// parameter, refreshed as the source lists it again, and the id the server knows it by as its
/// <c>ServerId</c> parameter.
/// </param>
public sealed record ExternalServerSection(string Slug, string Title, Type Component);

/// <summary>What can be done with a <see cref="ExternalServer"/> beyond seeing its status.</summary>
[Flags]
public enum ExternalServerFeatures
{
    None = 0,

    /// <summary>Its output can be read; see <see cref="IServerSourceExtension.ReadLogAsync"/>.</summary>
    Log = 1,

    /// <summary>Commands can be sent to it while it runs; see <see cref="IServerSourceExtension.SendCommandAsync"/>.</summary>
    Commands = 2,

    /// <summary>Its CPU, memory and uptime can be sampled; see <see cref="IServerSourceExtension.GetProcessInfoAsync"/>.</summary>
    ProcessInfo = 4,
}

/// <summary>A game server listed by an <see cref="IServerSourceExtension"/>.</summary>
public sealed record ExternalServer
{
    /// <summary>
    /// Identifies the server among the source's, and must stay the same for as long as it is the same
    /// server, e.g. its id where the source got it from. Case sensitive.
    /// </summary>
    public string Key { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>
    /// The game the server is for, when it is one of this server's games; a game that doesn't exist
    /// here is ignored.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>The host name or IP address players connect to; empty for the LANCommander server's own address.</summary>
    public string Host { get; init; } = "";

    /// <summary>The port players connect to; 0 when there isn't one.</summary>
    public int Port { get; init; }

    public ServerEngineStatus Status { get; init; } = ServerEngineStatus.Stopped;

    /// <summary>Why the server is in <see cref="ServerEngineStatus.Error"/>, where the source knows.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Where the server runs, shown beside it, e.g. the host it is on. Optional.</summary>
    public string Location { get; init; } = "";

    /// <summary>
    /// Where an administrator manages the server, such as a page of the plugin's or another system's
    /// page for it: an absolute URL, or a path on this server. The server's Edit button opens it; null
    /// leaves the server without one.
    /// </summary>
    public string? ManageUrl { get; init; }

    /// <summary>Whether administrators may start the server through <see cref="IServerSourceExtension.StartAsync"/>.</summary>
    public bool CanStart { get; init; }

    /// <summary>Whether administrators may stop the server through <see cref="IServerSourceExtension.StopAsync"/>.</summary>
    public bool CanStop { get; init; }

    /// <summary>What its Monitor page can show and do beyond its status.</summary>
    public ExternalServerFeatures Features { get; init; }
}

/// <summary>How an <see cref="IServerSourceExtension"/> reports changes between refreshes. Thread safe.</summary>
public interface IServerSourceHost
{
    /// <summary>Asks for the source's servers to be listed again now, e.g. because one was added.</summary>
    void Refresh();

    /// <summary>
    /// Reports that a listed server's status changed, so those watching it see so at once rather than
    /// at the next refresh. Does nothing for a key that wasn't listed.
    /// </summary>
    void ReportStatus(string key, ServerEngineStatus status, string? error = null);
}

/// <summary>Works out the ids of servers listed by an <see cref="IServerSourceExtension"/>.</summary>
public static class ExternalServerIds
{
    // Name-based (version 5) ids within a namespace of our own, so they never meet a database's
    private static readonly Guid Namespace = new("6f1e4d0a-52c3-4b8e-9a51-1c7d2b9e0f43");

    /// <summary>
    /// The id the server knows a listed server by: the same for the same source id (ignoring case) and
    /// key, and different for any other.
    /// </summary>
    public static Guid Create(string sourceId, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceId);
        ArgumentException.ThrowIfNullOrEmpty(key);

        var namespaceBytes = Namespace.ToByteArray(bigEndian: true);
        var name = Encoding.UTF8.GetBytes($"{sourceId.ToLowerInvariant()}\n{key}");

        var hash = SHA1.HashData([.. namespaceBytes, .. name]);
        var bytes = hash.AsSpan(0, 16).ToArray();

        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes, bigEndian: true);
    }
}
