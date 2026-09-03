using System.Net;
using System.Net.Sockets;
using LANCommander.HQ.SDK;

namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Maps an exception from an HQ call onto a connection status. Kept as a pure static so the
/// "unreachable is not unauthenticated" rule is directly unit-testable without any HQ plumbing.
/// </summary>
internal static class HqStatusMapper
{
    /// <summary>
    /// Maps <paramref name="exception"/> to a status, or null when it carries no signal about the
    /// connection at all (e.g. a bug in our own code) and existing state should be left alone.
    /// </summary>
    public static HqConnectionStatus? Map(Exception exception) => exception switch
    {
        // Must precede HQApiException: HQAuthenticationException derives from it, so the broader
        // arm would otherwise swallow the terminal case.
        //
        // This is the SDK saying the credential is finished — revoked, expired past its inactivity
        // window, or rejected as reused. It has already cleared the token store; no retry helps.
        HQAuthenticationException => HqConnectionStatus.Unauthorized,

        // HQApiException is thrown only when a response was received, so its StatusCode is an
        // exact signal rather than a guess.
        HQApiException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }
            => HqConnectionStatus.Unauthorized,

        // Any other HTTP status (5xx, 429, 404...) means HQ answered but couldn't serve us. The
        // credential is not implicated.
        HQApiException => HqConnectionStatus.Unreachable,

        HttpRequestException or SocketException or TimeoutException or TaskCanceledException
            => HqConnectionStatus.Unreachable,

        _ => null,
    };

    /// <summary>Whether the credential is gone for good and only an operator can restore it.</summary>
    public static bool IsTerminal(Exception exception) => exception is HQAuthenticationException;

    public static string Describe(Exception exception) => exception switch
    {
        HQAuthenticationException auth =>
            $"LANCommander HQ ended this server's session ({(int)auth.StatusCode}). Reconnect to restore access.",
        HQApiException api => $"LANCommander HQ returned {(int)api.StatusCode} ({api.StatusCode}).",
        _ => exception.Message,
    };
}
