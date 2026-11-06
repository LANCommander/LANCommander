namespace LANCommander.SDK.Enums
{
    /// <summary>What other players see of what a player is doing.</summary>
    public enum PresenceVisibility
    {
        /// <summary>The game and its rich presence: details, state, party and timer.</summary>
        Visible,

        /// <summary>The game only, without the rich presence the game reports.</summary>
        GameOnly,

        /// <summary>Nothing: the player never shows as playing.</summary>
        Hidden,
    }
}
