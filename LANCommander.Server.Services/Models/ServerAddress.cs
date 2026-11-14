namespace LANCommander.Server.Services.Models;

/// <summary>
/// Where players connect to a game server. An empty host is the LANCommander server's own address;
/// a port of 0 is no port in particular.
/// </summary>
public sealed record ServerAddress(string Host, int Port)
{
    /// <summary>
    /// The address players use: the server's own Host and Port where the administrator set them,
    /// since those override, and what its engine reported for those left blank.
    /// </summary>
    public static ServerAddress Resolve(string? host, int port, ReportedServerAddress? reported) => new(
        !string.IsNullOrWhiteSpace(host) ? host : reported?.Host ?? "",
        port > 0 ? port : reported?.Port ?? 0);

    /// <summary>The address as players would type it, e.g. <c>10.0.0.5:27015</c>; empty when there is no host.</summary>
    public override string ToString() =>
        string.IsNullOrWhiteSpace(Host) ? "" : Port > 0 ? $"{Host}:{Port}" : Host;
}

/// <summary>
/// What a server's engine last reported of where players connect to it. A null part wasn't
/// reported; an empty host is the LANCommander server's own address.
/// </summary>
public sealed record ReportedServerAddress(string? Host, int? Port);
