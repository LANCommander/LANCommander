using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.Launcher.Services.Platform;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.ViewModels;

/// <summary>A value the player has not set, and where it comes from.</summary>
/// <param name="Value">The value in its stored form, or null for none.</param>
/// <param name="Source">Where it was set -- "the server", "your launcher settings" -- or null when it is the built-in default.</param>
public sealed record InheritedOptionValue(string? Value, string? Source);

/// <summary>
/// Edits one layer of Interposer option values -- a game's, in its Manage dialog, or the launcher's
/// for every game, in Settings -- over whatever that layer inherits. Every option starts at Default,
/// showing what it inherits; only options the player changes are stored. The options most players
/// want come first; the rest of the schema follows, grouped as it is written, under Advanced.
/// </summary>
public partial class InterposerSettingsViewModel : ViewModelBase
{
    /// <summary>The options shown first, with names a player recognises, in this order.</summary>
    private static readonly (string Key, string Label, string Description)[] FeaturedOptions =
    [
        ("NetworkRelay.Enabled", "Network relay",
            "Play LAN modes with players on other networks, through the server's network relay. Other players see you under your alias."),
        ("RichPresence.Discord.Enabled", "Discord rich presence from the game",
            "Show the game in your Discord activity from inside the game, where plugins can add details such as the current map. The launcher's own presence stays off while this is on."),
        ("OsVersion.Version", "Reported Windows version",
            "For older games that check the Windows version and fail on anything newer than they know. Works like a compatibility mode, without running the game elevated."),
        ("DirectInput.FixLegacyDeviceEnumeration", "Fix DirectInput startup hangs",
            "For DirectInput 3 and 7 games that hang or crash while looking for controllers on startup."),
    ];

    /// <summary>
    /// Never offered here: the redistributable's install settings, what the server authors for a game
    /// (redirects, the virtual registry), and what the launcher supplies itself each launch (the
    /// player's alias, the relay session). An entry ending in a dot hides a whole group; any other, one option.
    /// </summary>
    private static readonly string[] Hidden =
    [
        "Loader.", "FileRedirects", "DnsRedirects", "Registry.", "Player.Username",
        "NetworkRelay.Server", "NetworkRelay.Port", "NetworkRelay.Network", "NetworkRelay.Ticket", "NetworkRelay.Address",

        // Edited as choices of this machine's devices, under Devices
        "NetworkAdapters.", "AudioDevices.", "DirectInput.DeviceFilter.",
    ];

    [ObservableProperty]
    private string? _notice;

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    partial void OnNoticeChanged(string? value) => OnPropertyChanged(nameof(HasNotice));

    public ObservableCollection<InterposerOptionRowViewModel> Featured { get; } = new();

    /// <summary>Which network adapters, controllers and audio devices the game sees.</summary>
    public ObservableCollection<InterposerDeviceFilterViewModel> Devices { get; } = new();

    public ObservableCollection<InterposerOptionGroupViewModel> Advanced { get; } = new();

    /// <summary>Lists this machine's devices for the filters to offer. Until then they keep what is stored.</summary>
    public Task LoadDevicesAsync(IDeviceCatalog catalog) =>
        Task.WhenAll(Devices.Select(filter => filter.LoadDevicesAsync(catalog)));

    /// <summary>
    /// Builds the editor for the layer <paramref name="values"/>, over <paramref name="inherit"/>.
    /// </summary>
    /// <param name="values">The layer being edited.</param>
    /// <param name="inherit">What an option the layer does not set comes to.</param>
    /// <param name="exclude">Options not to offer at all.</param>
    public static InterposerSettingsViewModel Build(
        IReadOnlyDictionary<string, string> values,
        Func<string, InheritedOptionValue> inherit,
        params string[] exclude)
    {
        var vm = new InterposerSettingsViewModel();
        var definitions = InterposerOptions.Definitions;

        bool Offered(string key) =>
            !exclude.Contains(key) && !Hidden.Any(hidden =>
                hidden.EndsWith('.') ? key.StartsWith(hidden, StringComparison.Ordinal) : key == hidden);

        foreach (var (key, label, description) in FeaturedOptions)
        {
            if (Offered(key) && definitions.TryGetValue(key, out var definition))
                vm.Featured.Add(InterposerOptionRowViewModel.Create(key, label, description, definition, values, inherit(key)));
        }

        vm.Devices.Add(new InterposerNetworkAdapterFilterViewModel(values, inherit));
        vm.Devices.Add(new InterposerControllerFilterViewModel(values, inherit));
        vm.Devices.Add(new InterposerAudioFilterViewModel(values, inherit));

        // One Advanced group per top-level schema group. Options of a group nested inside it are
        // labelled with that group's name: "Device Filter: Enabled".
        foreach (var (groupKey, group) in InterposerOptions.Schema.Options)
        {
            var rows = new List<InterposerOptionRowViewModel>();

            if (!string.IsNullOrWhiteSpace(group.Type))
                AddRows(rows, group, groupKey, prefix: null);
            else
                foreach (var (childKey, child) in group.Options ?? [])
                    AddRows(rows, child, $"{groupKey}.{childKey}", prefix: null);

            if (rows.Count > 0)
                vm.Advanced.Add(new InterposerOptionGroupViewModel(Label(group, groupKey), rows));
        }

        return vm;

        void AddRows(List<InterposerOptionRowViewModel> rows, OptionDefinition definition, string key, string? prefix)
        {
            if (!string.IsNullOrWhiteSpace(definition.Type))
            {
                if (Offered(key) && !FeaturedOptions.Any(f => f.Key == key))
                {
                    var label = prefix == null ? Label(definition, key) : $"{prefix}: {Label(definition, key)}";
                    rows.Add(InterposerOptionRowViewModel.Create(key, label, definition.Description, definition, values, inherit(key)));
                }

                return;
            }

            var childPrefix = prefix == null ? Label(definition, key) : $"{prefix}: {Label(definition, key)}";

            foreach (var (childKey, child) in definition.Options ?? [])
                AddRows(rows, child, $"{key}.{childKey}", childPrefix);
        }
    }

    private static string Label(OptionDefinition definition, string key) =>
        !string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.DisplayName : key[(key.LastIndexOf('.') + 1)..];

    /// <summary>
    /// The values the player set, by dotted key. Options left at Default are left out. A null value takes
    /// the key out of the game's config, for a setting armed by the key's presence alone -- an audio
    /// direction the player chose not to filter, where the server filters it.
    /// </summary>
    public Dictionary<string, string?> CollectValues()
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var row in Featured.Concat(Advanced.SelectMany(g => g.Rows)))
        {
            if (row.GetValue() is { } value)
                values[row.Key] = value;
        }

        foreach (var filter in Devices)
            filter.CollectInto(values);

        return values;
    }
}

public sealed class InterposerOptionGroupViewModel(string displayName, IEnumerable<InterposerOptionRowViewModel> rows)
{
    public string DisplayName { get; } = displayName;

    public ObservableCollection<InterposerOptionRowViewModel> Rows { get; } = new(rows);
}

/// <summary>One Interposer option, at Default or set to a value of the player's.</summary>
public abstract partial class InterposerOptionRowViewModel : ViewModelBase
{
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>Where the inherited value comes from, for a caption under the option; null when it is built in.</summary>
    public string? InheritedSource { get; init; }

    public string? InheritedSourceText => InheritedSource == null ? null : $"Default set by {InheritedSource}";

    public bool HasInheritedSource => InheritedSource != null;

    /// <summary>The player's value in stored form, or null when the option is at Default.</summary>
    public abstract string? GetValue();

    internal static InterposerOptionRowViewModel Create(
        string key, string label, string? description, OptionDefinition definition,
        IReadOnlyDictionary<string, string> values, InheritedOptionValue inherited)
    {
        values.TryGetValue(key, out var value);

        switch ((definition.Type ?? "string").ToLowerInvariant())
        {
            case "bool":
                return new InterposerChoiceRowViewModel(
                    [new("true", "On"), new("false", "Off")], value, inherited, isBoolean: true)
                {
                    Key = key, DisplayName = label, Description = description, InheritedSource = inherited.Source,
                };

            case "choice":
            case "enum":
                return new InterposerChoiceRowViewModel(
                    (definition.Choices ?? []).Select(c => new InterposerChoiceItem(c.Value, c.Label)).ToList(), value, inherited, isBoolean: false)
                {
                    Key = key, DisplayName = label, Description = description, InheritedSource = inherited.Source,
                };

            case "int":
                return new InterposerNumberRowViewModel(value, inherited)
                {
                    Key = key, DisplayName = label, Description = description, InheritedSource = inherited.Source,
                };

            case "list":
                return new InterposerListRowViewModel(definition, key, value, inherited)
                {
                    Key = key, DisplayName = label, Description = description, InheritedSource = inherited.Source,
                };

            default:
                return new InterposerTextRowViewModel(value, inherited)
                {
                    Key = key, DisplayName = label, Description = description, InheritedSource = inherited.Source,
                };
        }
    }
}

/// <param name="Value">The stored value; null for the Default entry.</param>
public sealed record InterposerChoiceItem(string? Value, string Label);

/// <summary>A switch or a choice: Default first, showing what it comes to, then each value.</summary>
public partial class InterposerChoiceRowViewModel : InterposerOptionRowViewModel
{
    public ObservableCollection<InterposerChoiceItem> Items { get; }

    [ObservableProperty]
    private InterposerChoiceItem _selectedItem;

    internal InterposerChoiceRowViewModel(IReadOnlyList<InterposerChoiceItem> choices, string? value, InheritedOptionValue inherited, bool isBoolean)
    {
        string Describe(string? stored)
        {
            if (isBoolean)
                return bool.TryParse(stored, out var on) && on ? "On" : "Off";

            return choices.FirstOrDefault(c => c.Value == stored)?.Label ?? (string.IsNullOrEmpty(stored) ? "None" : stored);
        }

        var defaultItem = new InterposerChoiceItem(null, $"Default ({Describe(inherited.Value)})");

        Items = new ObservableCollection<InterposerChoiceItem>([defaultItem, .. choices]);

        // A stored boolean may be spelled any way bool.Parse accepts
        var stored = isBoolean && bool.TryParse(value, out var parsed) ? (parsed ? "true" : "false") : value;

        _selectedItem = Items.Skip(1).FirstOrDefault(i => i.Value == stored) ?? defaultItem;
    }

    public override string? GetValue() => SelectedItem?.Value;
}

/// <summary>Text: empty is Default, and the Default shows as the box's placeholder.</summary>
public partial class InterposerTextRowViewModel : InterposerOptionRowViewModel
{
    [ObservableProperty]
    private string _text;

    public string Watermark { get; }

    internal InterposerTextRowViewModel(string? value, InheritedOptionValue inherited)
    {
        _text = value ?? string.Empty;
        Watermark = string.IsNullOrEmpty(inherited.Value) ? "Default" : $"Default: {inherited.Value}";
    }

    public override string? GetValue() => string.IsNullOrEmpty(Text) ? null : Text;
}

/// <summary>A number: cleared is Default, and the Default shows as the box's placeholder.</summary>
public partial class InterposerNumberRowViewModel : InterposerOptionRowViewModel
{
    [ObservableProperty]
    private decimal? _value;

    public string Watermark { get; }

    internal InterposerNumberRowViewModel(string? value, InheritedOptionValue inherited)
    {
        if (decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            _value = number;

        Watermark = string.IsNullOrEmpty(inherited.Value) ? "Default" : $"Default: {inherited.Value}";
    }

    public override string? GetValue() =>
        Value.HasValue ? ((long)Value.Value).ToString(CultureInfo.InvariantCulture) : null;
}

/// <summary>
/// A list: at Default until the player chooses to set it, then edited from the list it inherits.
/// An empty list is a value of its own -- for the device filters, "hide them all".
/// </summary>
public partial class InterposerListRowViewModel : InterposerOptionRowViewModel
{
    [ObservableProperty]
    private bool _isSet;

    public GameOptionListViewModel List { get; }

    public string InheritedSummary { get; }

    internal InterposerListRowViewModel(OptionDefinition definition, string key, string? value, InheritedOptionValue inherited)
    {
        _isSet = value != null;

        // Starts from the player's list, or else the one it inherits, so setting it begins from what applies now
        List = new GameOptionListViewModel(definition, key, string.Empty, value ?? inherited.Value);

        InheritedSummary = CountItems(inherited.Value) switch
        {
            null => "Default: not set",
            1 => "Default: 1 item",
            var count => $"Default: {count} items",
        };
    }

    /// <summary>How many items a stored list holds, or null when there is no list at all.</summary>
    private static int? CountItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);

            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array ? document.RootElement.GetArrayLength() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public override string? GetValue() => IsSet ? List.GetValue() : null;
}
