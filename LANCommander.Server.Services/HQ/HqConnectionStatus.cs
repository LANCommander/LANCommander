namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Runtime health of the server's link to LANCommander HQ. Deliberately more than a bool: a network
/// blip and a revoked token demand very different responses, and collapsing them is what let an
/// expired credential masquerade as a working connection.
/// </summary>
public enum HqConnectionStatus
{
    /// <summary>No credential is stored.</summary>
    Disconnected,

    /// <summary>A credential is stored but has not been verified yet this process lifetime.</summary>
    Unknown,

    /// <summary>Verified against HQ.</summary>
    Connected,

    /// <summary>HQ rejected the token (401/403), or it is locally past its <c>exp</c>.</summary>
    Unauthorized,

    /// <summary>HQ could not be reached. The credential may still be perfectly good.</summary>
    Unreachable,
}
