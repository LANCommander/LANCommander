using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>
    /// How far a reporter's events can be believed. Decided by the LANCommander server from how the
    /// reporter authenticated, never claimed by the reporter.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MatchReporterTrust
    {
        /// <summary>A player's own client. Its events describe what it saw.</summary>
        Untrusted,

        /// <summary>A player hosting the match.</summary>
        SemiTrusted,

        /// <summary>An administrator, a token with the match reporting scope, or a plugin.</summary>
        Authoritative,
    }
}
