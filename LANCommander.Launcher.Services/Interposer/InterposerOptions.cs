using LANCommander.Interposer;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// One game's Interposer option values, layer by layer: the schema's defaults, the values the server
/// set on the game's Interposer redistributable, the launcher's values for every game, and the
/// player's values for this game. Later layers win; the player's always do. Keys are the dotted
/// option keys of the redistributable's schema, which are Config.yml paths.
/// </summary>
public sealed class InterposerOptions
{
    /// <summary>Options only the redistributable's Install script reads; never part of the DLL's config.</summary>
    private const string LoaderGroup = "Loader.";

    private static readonly Lazy<OptionSchema> LazySchema = new(() =>
        new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .WithTypeConverter(new OptionChoiceYamlConverter())
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<OptionSchema>(InterposerOptionSchema.Yaml));

    private static readonly Lazy<IReadOnlyDictionary<string, OptionDefinition>> LazyDefinitions = new(() => LazySchema.Value.GetFlattenedOptions());

    /// <summary>The Interposer redistributable's id, which keys its option values everywhere.</summary>
    public static Guid RedistributableId => InterposerOptionSchema.RedistributableId;

    /// <summary>The schema of the Interposer the launcher bundles, as the tree of groups it is written as.</summary>
    public static OptionSchema Schema => LazySchema.Value;

    /// <summary>The schema of the Interposer the launcher bundles, flattened to dotted keys.</summary>
    public static IReadOnlyDictionary<string, OptionDefinition> Definitions => LazyDefinitions.Value;

    private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

    public InterposerOptions(
        IReadOnlyDictionary<string, string>? server,
        IReadOnlyDictionary<string, string>? global,
        IReadOnlyDictionary<string, string>? player,
        bool hasRedistributable)
    {
        Server = server ?? None;
        Global = global ?? None;
        Player = player ?? None;
        HasRedistributable = hasRedistributable;
    }

    /// <summary>
    /// The options for a game about to run. The player's values come from the manifest, where the
    /// launcher writes them before each launch.
    /// </summary>
    public static InterposerOptions For(SDK.Models.Manifest.Game? manifest, InterposerSettings? settings)
    {
        var redistributable = manifest?.Redistributables?.FirstOrDefault(r => r.Id == RedistributableId);

        Dictionary<string, string>? player = null;
        manifest?.RedistributableOptions?.TryGetValue(RedistributableId, out player);

        return new InterposerOptions(redistributable?.Options, settings?.Options, player, redistributable != null);
    }

    /// <summary>Whether the server installs the Interposer redistributable with this game.</summary>
    public bool HasRedistributable { get; }

    /// <summary>The server's values for the game's Interposer redistributable.</summary>
    public IReadOnlyDictionary<string, string> Server { get; }

    /// <summary>The launcher's values for every game.</summary>
    public IReadOnlyDictionary<string, string> Global { get; }

    /// <summary>The player's values for this game.</summary>
    public IReadOnlyDictionary<string, string> Player { get; }

    /// <summary>The value the game runs with: the player's, else the launcher's, else the server's, else the default.</summary>
    public string? Get(string key)
    {
        if (Player.TryGetValue(key, out var value) || Global.TryGetValue(key, out value) || Server.TryGetValue(key, out value))
            return value;

        return GetDefault(key);
    }

    public bool IsEnabled(string key) => bool.TryParse(Get(key), out var enabled) && enabled;

    public static string? GetDefault(string key) =>
        Definitions.TryGetValue(key, out var definition) ? definition.GetDefaultAsString() : null;

    /// <summary>
    /// The values to layer over the DLL's own Config.yml for this launch. A copy the redistributable
    /// installed already has the server's values in its Config.yml; the launcher's own copy has no
    /// Config.yml at all, so it is given them here. A null value removes the key from the config.
    /// </summary>
    public Dictionary<string, string?> GetOverlayValues(bool bundled)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        void Add(IReadOnlyDictionary<string, string> layer)
        {
            foreach (var (key, value) in layer)
            {
                if (!key.StartsWith(LoaderGroup, StringComparison.Ordinal))
                    values[key] = value;
            }
        }

        if (bundled)
            Add(Server);

        Add(Global);
        Add(Player);

        return values;
    }

    /// <summary>
    /// Whether this launch's overlay would change anything the DLL does on its own: for the launcher's
    /// copy, compared with the schema's defaults; for the redistributable's, with the server's values,
    /// which its Config.yml already holds.
    /// </summary>
    public bool ChangesConfig(bool bundled) => ChangesConfig(GetOverlayValues(bundled), bundled);

    /// <inheritdoc cref="ChangesConfig(bool)"/>
    /// <param name="overlay">The overlay as it will be sent, after any adjustment to <see cref="GetOverlayValues"/>.</param>
    /// <param name="bundled">Whether it goes to the launcher's copy.</param>
    public bool ChangesConfig(IReadOnlyDictionary<string, string?> overlay, bool bundled)
    {
        foreach (var (key, value) in overlay)
        {
            var current = !bundled && Server.TryGetValue(key, out var server) ? server : GetDefault(key);

            if (!SameValue(current, value))
                return true;
        }

        return false;
    }

    private static bool SameValue(string? a, string? b)
    {
        // Unset and blank mean the same to the DLL: no value of its own, so its built-in behaviour
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b);

        if (bool.TryParse(a, out var boolA) && bool.TryParse(b, out var boolB))
            return boolA == boolB;

        return string.Equals(a, b, StringComparison.Ordinal);
    }
}
