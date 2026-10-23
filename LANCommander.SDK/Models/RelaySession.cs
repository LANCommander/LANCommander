using System;

namespace LANCommander.SDK.Models
{
    /// <summary>
    /// Asks the server's network relay to let the signed-in player into a game's relay network.
    /// Posted to <see cref="Services.RelayClient.SessionsRoute"/>.
    /// </summary>
    public class RelaySessionRequest
    {
        /// <summary>The game about to be played. Players of the same game share a relay network.</summary>
        public Guid GameId { get; set; }

        /// <summary>The game's title, so the server's administrators can tell its relay network apart. Display only.</summary>
        public string GameTitle { get; set; }
    }

    /// <summary>
    /// What a game's Interposer needs to join the server's network relay: where the relay listens,
    /// the network to join, and a ticket proving the server let this player in.
    /// </summary>
    public class RelaySession
    {
        /// <summary>
        /// The host the relay listens on, when it is not the host the launcher reaches the server at
        /// (for example behind a reverse proxy that only carries HTTP). Null means the server's host.
        /// </summary>
        public string Host { get; set; }

        /// <summary>The relay's TCP port, which is also its UDP port.</summary>
        public int Port { get; set; }

        /// <summary>The relay network to join; the ticket only admits this one.</summary>
        public string Network { get; set; } = string.Empty;

        /// <summary>
        /// Passed to the Interposer as <c>NetworkRelay.Ticket</c>. Opaque to the launcher; it is only
        /// good for the relay, not for the server's API.
        /// </summary>
        public string Ticket { get; set; } = string.Empty;

        /// <summary>When the ticket stops admitting the player, including on reconnect.</summary>
        public DateTimeOffset ExpiresOn { get; set; }
    }
}
