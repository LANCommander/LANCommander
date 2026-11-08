using System.Text.Json.Serialization;

namespace LANCommander.SDK.Enums
{
    /// <summary>Where a link between an in-game identity and a LANCommander user came from.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PlayerIdentitySource
    {
        /// <summary>An administrator or the user added it.</summary>
        Manual,

        /// <summary>The user's launcher reported it.</summary>
        Launcher,

        /// <summary>A plugin added it.</summary>
        Plugin,

        /// <summary>The server inferred it from the user playing on a match's server under that name.</summary>
        Inferred,
    }
}
