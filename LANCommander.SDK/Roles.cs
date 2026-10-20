namespace LANCommander.SDK
{
    /// <summary>
    /// Server role names that clients need to reason about.
    /// </summary>
    /// <remarks>
    /// Shared so a client-side capability check and the server-side authorization policy it
    /// mirrors cannot drift apart.
    /// </remarks>
    public static class Roles
    {
        /// <summary>
        /// Full administrative access. Required by the upload and import endpoints, and
        /// therefore by anything that creates or edits games from a client.
        /// </summary>
        public const string Administrator = "Administrator";
    }
}
