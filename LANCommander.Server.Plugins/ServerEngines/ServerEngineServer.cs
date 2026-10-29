using System.Text.Json;

namespace LANCommander.Server.Plugins;

/// <summary>A game server as an <see cref="IServerEngineExtension"/> sees it.</summary>
public sealed record ServerEngineServer
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    /// <summary>The game the server is for, if any.</summary>
    public Guid? GameId { get; init; }

    /// <summary>The address players connect to.</summary>
    public string Host { get; init; } = "";

    public int Port { get; init; }

    /// <summary>
    /// What the engine keeps about the server, as JSON, or empty when it has kept nothing yet. Read it
    /// with <see cref="GetSettings{T}"/>.
    /// </summary>
    public string Settings { get; init; } = "";

    /// <summary>
    /// <see cref="Settings"/> read as <typeparamref name="T"/>, or a new <typeparamref name="T"/> when
    /// there are none or they can't be read.
    /// </summary>
    public T GetSettings<T>() where T : class, new() => ServerEngineSettings.Read<T>(Settings);
}

/// <summary>Reads and writes <see cref="ServerEngineServer.Settings"/>.</summary>
public static class ServerEngineSettings
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Reads settings JSON as <typeparamref name="T"/>; a new one when it is empty or can't be read.</summary>
    public static T Read<T>(string? json) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(json))
            return new T();

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    /// <summary>Writes settings as JSON.</summary>
    public static string Write<T>(T settings) => JsonSerializer.Serialize(settings, Options);
}
