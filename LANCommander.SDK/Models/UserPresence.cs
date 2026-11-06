using System;

namespace LANCommander.SDK.Models
{
    /// <summary>A player who is in a game right now.</summary>
    public class UserPresence
    {
        public Guid UserId { get; set; }

        /// <summary>The player's alias, or their user name when they have none.</summary>
        public string UserName { get; set; }

        public Guid GameId { get; set; }

        public string GameTitle { get; set; }

        /// <summary>The game's icon on the server, to fetch from <c>/api/Media/{id}/Thumbnail</c>.</summary>
        public Guid? IconMediaId { get; set; }

        /// <summary>When the play session started, UTC.</summary>
        public DateTime StartedOn { get; set; }

        /// <summary>What the game reports the player is doing, or null when it reports nothing.</summary>
        public PresenceActivity Activity { get; set; }
    }

    /// <summary>
    /// The rich presence a game reports through the Interposer: the same fields a game shows
    /// in Discord, minus what only Discord can resolve.
    /// </summary>
    public class PresenceActivity
    {
        /// <summary>First detail line, e.g. the map.</summary>
        public string Details { get; set; }

        /// <summary>Second detail line, e.g. the score.</summary>
        public string State { get; set; }

        /// <summary>When the current activity started, for an elapsed timer.</summary>
        public DateTimeOffset? StartsOn { get; set; }

        /// <summary>When the current activity ends, for a countdown.</summary>
        public DateTimeOffset? EndsOn { get; set; }

        /// <summary>Players in the party; 0 when the game reports none.</summary>
        public int PartySize { get; set; }

        /// <summary>The party's capacity; 0 when the game reports none.</summary>
        public int PartyMax { get; set; }

        /// <summary>
        /// The large image as an <c>http(s)</c> URL, or null. Discord asset keys never reach
        /// here: nothing outside Discord can resolve them.
        /// </summary>
        public string LargeImageUrl { get; set; }

        public string LargeImageText { get; set; }

        /// <summary>The small image as an <c>http(s)</c> URL, or null.</summary>
        public string SmallImageUrl { get; set; }

        public string SmallImageText { get; set; }

        /// <summary>Whether there is anything to show beyond the game itself.</summary>
        public bool HasContent =>
            !String.IsNullOrWhiteSpace(Details)
            || !String.IsNullOrWhiteSpace(State)
            || StartsOn.HasValue
            || EndsOn.HasValue
            || PartyMax > 0
            || !String.IsNullOrWhiteSpace(LargeImageUrl);
    }
}
