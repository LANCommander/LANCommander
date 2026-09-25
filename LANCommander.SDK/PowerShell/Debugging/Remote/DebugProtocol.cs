#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>Constants and helpers shared by both ends of the debug pipe.</summary>
public static class DebugProtocol
{
    public const int Version = 1;

    /// <summary>Upper bound on a single frame. Variable and command lists are the big ones.</summary>
    public const int MaxFrameLength = 16 * 1024 * 1024;

    /// <summary>How long a child waits for the launcher to accept its connection.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Extra time a proxy waits beyond the engine's own timeout before giving up on a reply.</summary>
    public static readonly TimeSpan ReplyGrace = TimeSpan.FromSeconds(2);

    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string BuildPipeName(int parentProcessId, Guid sessionId) =>
        $"LANCommander.ScriptDebug.{parentProcessId}.{sessionId:N}";

    public static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Compare tokens in constant time so a local attacker cannot guess one byte at a time.</summary>
    public static bool TokensMatch(string? presented, string? expected)
    {
        if (presented is null || expected is null)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(expected));
    }
}
