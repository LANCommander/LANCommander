using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services
{
    /// <summary>
    /// Talks to the server's network relay, which the Relay server plugin provides: asks it to let
    /// the player into a game's relay network.
    /// </summary>
    public class RelayClient(
        ApiRequestFactory apiRequestFactory,
        IConnectionClient connectionClient,
        ILogger<RelayClient> logger)
    {
        /// <summary>Where the Relay plugin takes <see cref="RelaySessionRequest"/>s.</summary>
        public const string SessionsRoute = "/api/Plugins/Relay/Sessions";

        /// <summary>
        /// Whether a game can use the relay: it has a LAN multiplayer mode over TCP/IP or IPX, the
        /// protocols the Interposer carries.
        /// </summary>
        public static bool SupportsRelay(Models.Manifest.Game manifest) =>
            manifest?.MultiplayerModes?.Any(mode =>
                mode.Type == MultiplayerType.LAN
                && mode.NetworkProtocol is NetworkProtocol.TCPIP or NetworkProtocol.IPX) == true;

        /// <summary>
        /// A relay session for the game, or null when there is none to be had: the launcher is
        /// offline, the server has no relay (the plugin is not installed, or the relay is off), or the
        /// server could not be reached. A missing relay never stops a game from starting.
        /// </summary>
        public virtual async Task<RelaySession> CreateSessionAsync(Guid gameId, string gameTitle = null, CancellationToken cancellationToken = default)
        {
            if (!connectionClient.IsConnected())
                return null;

            try
            {
                var session = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseCancellationToken(cancellationToken)
                    .UseRoute(SessionsRoute)
                    .AddBody(new RelaySessionRequest { GameId = gameId, GameTitle = gameTitle })
                    .PostAsync<RelaySession>();

                // A server that sent something other than a session (a sign-in page, say) has none to give
                if (session == null || session.Port <= 0 || string.IsNullOrWhiteSpace(session.Ticket))
                {
                    logger?.LogDebug("The server did not return a relay session for game {GameId}", gameId);
                    return null;
                }

                return session;
            }
            catch (HttpRequestException ex)
            {
                // 404: no Relay plugin. 503: the relay is turned off or could not start.
                logger?.LogDebug(ex, "No relay session for game {GameId} ({StatusCode})", gameId, ex.StatusCode);
                return null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex, "Could not get a relay session for game {GameId}", gameId);
                return null;
            }
        }
    }
}
