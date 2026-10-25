using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services.Platform;

namespace LANCommander.Launcher.ViewModels;

public enum DeviceFilterMode
{
    /// <summary>Whatever the game inherits.</summary>
    Default,

    /// <summary>Filter off: the game sees every device.</summary>
    All,

    /// <summary>Filter on: the game sees only the devices picked.</summary>
    Selected,
}

public sealed record DeviceFilterModeItem(DeviceFilterMode Mode, string Label);

/// <summary>A device, or a rule naming one, that a filter can let through.</summary>
public partial class SelectableDeviceViewModel : ObservableObject
{
    public string Label { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public bool HasDetail => !string.IsNullOrWhiteSpace(Detail);

    /// <summary>What the filter stores for it.</summary>
    public string Entry { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// One of the Interposer's device filters -- network adapters, controllers or audio devices -- edited as
/// a choice of the devices on this machine rather than as the raw lists the filter stores. At Default
/// nothing is stored and the game gets what it inherits. Rules that name no device found here (a headset
/// that is unplugged, a name pattern the server wrote) are kept as they are.
/// </summary>
public abstract partial class InterposerDeviceFilterViewModel : ViewModelBase
{
    private readonly string _enabledKey;
    private readonly IReadOnlyDictionary<string, string> _values;
    private readonly Func<string, InheritedOptionValue> _inherit;

    protected InterposerDeviceFilterViewModel(
        string enabledKey,
        string[] listKeys,
        string title,
        string description,
        string selectedLabel,
        IReadOnlyDictionary<string, string> values,
        Func<string, InheritedOptionValue> inherit)
    {
        _enabledKey = enabledKey;
        _values = values;
        _inherit = inherit;

        Title = title;
        Description = description;

        var inheritedEnabled = inherit(enabledKey);
        var inheritsFilter = bool.TryParse(inheritedEnabled.Value, out var on) && on && listKeys.Any(key => inherit(key).Value != null);

        InheritedSource = inheritsFilter ? inheritedEnabled.Source : null;

        Modes =
        [
            new(DeviceFilterMode.Default, inheritsFilter ? "Default (only some shown)" : "Default (all shown)"),
            new(DeviceFilterMode.All, "Show all"),
            new(DeviceFilterMode.Selected, selectedLabel),
        ];

        // The player's choice: the switch itself, or failing that any list of theirs
        var mode = values.TryGetValue(enabledKey, out var enabled) && bool.TryParse(enabled, out var isOn)
            ? isOn ? DeviceFilterMode.Selected : DeviceFilterMode.All
            : listKeys.Any(values.ContainsKey) ? DeviceFilterMode.Selected : DeviceFilterMode.Default;

        _selectedMode = Modes.First(m => m.Mode == mode);
    }

    public string Title { get; }

    public string Description { get; }

    public ObservableCollection<DeviceFilterModeItem> Modes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelecting))]
    private DeviceFilterModeItem _selectedMode;

    public bool IsSelecting => SelectedMode.Mode == DeviceFilterMode.Selected;

    public string? InheritedSource { get; }

    public string? InheritedSourceText => InheritedSource == null ? null : $"Default set by {InheritedSource}";

    public bool HasInheritedSource => InheritedSource != null;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>Whether the devices have been listed. Until they are, the stored rules are kept as they are.</summary>
    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>
    /// What a list starts from: the player's, or else the one inherited, so picking devices begins from
    /// what applies now. Null when neither sets it.
    /// </summary>
    protected string? Seed(string key) => _values.TryGetValue(key, out var value) ? value : _inherit(key).Value;

    protected static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new();

        try
        {
            return JsonSerializer.Deserialize<List<string?>>(json)?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList() ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    protected static string ToJson(IEnumerable<string> entries) => JsonSerializer.Serialize(entries.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public async Task LoadDevicesAsync(IDeviceCatalog catalog)
    {
        IsLoading = true;

        try
        {
            await LoadAsync(catalog);
            IsLoaded = true;
            OnLoaded();
        }
        catch (Exception)
        {
            // Devices that cannot be listed leave the filter as it was stored, which is still saved as is
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Lists the devices off the UI thread, then fills in the choices on it.</summary>
    protected abstract Task LoadAsync(IDeviceCatalog catalog);

    /// <summary>Runs once the devices are listed and <see cref="IsLoaded"/> is set.</summary>
    protected virtual void OnLoaded()
    {
    }

    /// <summary>Adds what the filter stores to <paramref name="values"/>; nothing at Default.</summary>
    public void CollectInto(Dictionary<string, string?> values)
    {
        switch (SelectedMode.Mode)
        {
            case DeviceFilterMode.All:
                values[_enabledKey] = "false";
                break;

            case DeviceFilterMode.Selected:
                values[_enabledKey] = "true";
                CollectSelection(values);
                break;
        }
    }

    protected abstract void CollectSelection(Dictionary<string, string?> values);
}

/// <summary>
/// <c>NetworkAdapters</c>: which adapters the game sees. A picked adapter is stored as its IPv4 subnet,
/// the one rule every enumeration API honours, and its MAC address, which tells it from another adapter
/// on the same subnet. Name patterns are kept but not offered.
/// </summary>
public partial class InterposerNetworkAdapterFilterViewModel : InterposerDeviceFilterViewModel
{
    private const string Subnets = "NetworkAdapters.Subnets";
    private const string Macs = "NetworkAdapters.MACs";
    private const string Names = "NetworkAdapters.Names";

    private readonly List<string> _subnets;
    private readonly List<string> _macs;
    private readonly List<string> _names;

    // Subnets and MACs that match no adapter found here: kept on save
    private readonly List<string> _keptSubnets = new();
    private readonly List<string> _keptMacs = new();

    public InterposerNetworkAdapterFilterViewModel(IReadOnlyDictionary<string, string> values, Func<string, InheritedOptionValue> inherit)
        : base("NetworkAdapters.Enabled", [Subnets, Macs, Names],
            "Network adapters",
            "For games that pick the wrong adapter -- a VPN's, or a virtual machine's -- and so cannot find or be found by other players on the LAN.",
            "Show only the adapters I pick",
            values, inherit)
    {
        _subnets = ParseList(Seed(Subnets));
        _macs = ParseList(Seed(Macs));
        _names = ParseList(Seed(Names));
    }

    public ObservableCollection<SelectableDeviceViewModel> Adapters { get; } = new();

    /// <summary>Rules that name no adapter found here, kept as they are.</summary>
    public ObservableCollection<string> OtherRules { get; } = new();

    public bool HasOtherRules => OtherRules.Count > 0;

    protected override async Task LoadAsync(IDeviceCatalog catalog)
    {
        var adapters = await Task.Run(catalog.GetNetworkAdapters);

        Adapters.Clear();
        OtherRules.Clear();

        var matchedSubnets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedMacs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var adapter in adapters)
        {
            var mac = adapter.MacAddress;
            var subnet = adapter.Subnet;

            var byMac = mac != null && _macs.Any(m => SameMac(m, mac));
            var bySubnet = subnet != null && _subnets.Contains(subnet, StringComparer.OrdinalIgnoreCase);

            if (byMac) matchedMacs.UnionWith(_macs.Where(m => SameMac(m, mac!)));
            if (bySubnet) matchedSubnets.Add(subnet!);

            var details = new List<string>();

            if (!string.IsNullOrWhiteSpace(adapter.Description))
                details.Add(adapter.Description);

            if (adapter.IPv4Address != null)
                details.Add(adapter.IPv4Address);

            if (!adapter.IsConnected)
                details.Add("disconnected");

            Adapters.Add(new SelectableDeviceViewModel
            {
                Label = adapter.Name,
                Detail = string.Join(" · ", details),
                Entry = string.Join('|', subnet ?? string.Empty, mac ?? string.Empty),
                IsSelected = byMac || bySubnet,
            });
        }

        _keptSubnets.Clear();
        _keptSubnets.AddRange(_subnets.Where(s => !matchedSubnets.Contains(s)));

        _keptMacs.Clear();
        _keptMacs.AddRange(_macs.Where(m => !matchedMacs.Contains(m)));

        foreach (var subnet in _keptSubnets)
            OtherRules.Add($"Subnet {subnet}");

        foreach (var mac in _keptMacs)
            OtherRules.Add($"Adapter address {mac}");

        foreach (var name in _names)
            OtherRules.Add($"Name matching {name}");

        OnPropertyChanged(nameof(HasOtherRules));
    }

    protected override void CollectSelection(Dictionary<string, string?> values)
    {
        if (!IsLoaded)
        {
            values[Subnets] = ToJson(_subnets);
            values[Macs] = ToJson(_macs);
            values[Names] = ToJson(_names);
            return;
        }

        var picked = Adapters.Where(a => a.IsSelected).Select(a => a.Entry.Split('|')).ToList();

        values[Subnets] = ToJson(picked.Select(p => p[0]).Where(s => s.Length > 0).Concat(_keptSubnets));
        values[Macs] = ToJson(picked.Select(p => p[1]).Where(m => m.Length > 0).Concat(_keptMacs));
        values[Names] = ToJson(_names);
    }

    private static bool SameMac(string a, string b) =>
        string.Equals(a.Replace('-', ':'), b.Replace('-', ':'), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// <c>DirectInput.DeviceFilter</c>: which input devices a DirectInput game sees, by kind and by name. A
/// picked controller is stored as a pattern matching its product name exactly.
/// </summary>
public partial class InterposerControllerFilterViewModel : InterposerDeviceFilterViewModel
{
    private const string Classes = "DirectInput.DeviceFilter.Classes";
    private const string Names = "DirectInput.DeviceFilter.Names";

    private static readonly (string Class, string Label)[] DeviceClasses =
    [
        ("Mouse", "Mice"),
        ("Keyboard", "Keyboards"),
        ("Gamepad", "Gamepads"),
        ("Joystick", "Joysticks"),
        ("Driving", "Wheels and pedals"),
        ("Flight", "Flight sticks"),
        ("FirstPerson", "First-person controllers"),
        ("ScreenPointer", "Light guns and touch screens"),
        ("Remote", "Remote controls"),
        ("DeviceControl", "Control panels"),
        ("Supplemental", "Add-ons, such as rudder pedals"),
        ("Device", "Anything else"),
    ];

    private readonly List<string> _names;

    public InterposerControllerFilterViewModel(IReadOnlyDictionary<string, string> values, Func<string, InheritedOptionValue> inherit)
        : base("DirectInput.DeviceFilter.Enabled", [Classes, Names],
            "Controllers",
            "For DirectInput games that take the wrong controller.",
            "Show only the kinds and controllers I pick",
            values, inherit)
    {
        var classes = ParseList(Seed(Classes));
        _names = ParseList(Seed(Names));

        // Nothing picked lets everything through, which is not what choosing to pick means: start from
        // mouse and keyboard
        if (classes.Count == 0 && _names.Count == 0)
            classes = ["Mouse", "Keyboard"];

        foreach (var (deviceClass, label) in DeviceClasses)
        {
            Kinds.Add(new SelectableDeviceViewModel
            {
                Label = label,
                Entry = deviceClass,
                IsSelected = classes.Contains(deviceClass, StringComparer.OrdinalIgnoreCase),
            });
        }
    }

    public ObservableCollection<SelectableDeviceViewModel> Kinds { get; } = new();

    public ObservableCollection<SelectableDeviceViewModel> Controllers { get; } = new();

    public bool HasControllers => Controllers.Count > 0;

    public bool HasNoControllers => IsLoaded && Controllers.Count == 0;

    protected override async Task LoadAsync(IDeviceCatalog catalog)
    {
        var controllers = await Task.Run(catalog.GetControllers);

        Controllers.Clear();

        var matched = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controller in controllers.DistinctBy(c => c.ProductName, StringComparer.OrdinalIgnoreCase))
        {
            var pattern = ExactPattern(controller.ProductName);
            var instancePattern = ExactPattern(controller.InstanceName);
            var selected = _names.Contains(pattern) || _names.Contains(instancePattern);

            if (selected)
                matched.UnionWith(_names.Where(n => n == pattern || n == instancePattern));

            AddController(new SelectableDeviceViewModel
            {
                Label = controller.ProductName,
                Detail = controller.InstanceName != controller.ProductName ? controller.InstanceName : null,
                Entry = pattern,
                IsSelected = selected,
            });
        }

        // Rules for controllers not plugged in now, or patterns written by hand
        foreach (var name in _names.Where(n => !matched.Contains(n)))
        {
            AddController(new SelectableDeviceViewModel
            {
                Label = FromExactPattern(name) ?? name,
                Detail = FromExactPattern(name) != null ? "Not connected" : "Name pattern",
                Entry = name,
                IsSelected = true,
            });
        }

        OnPropertyChanged(nameof(HasControllers));
    }

    protected override void OnLoaded() => OnPropertyChanged(nameof(HasNoControllers));

    private void AddController(SelectableDeviceViewModel item) => Controllers.Add(item);

    protected override void CollectSelection(Dictionary<string, string?> values)
    {
        values[Classes] = ToJson(Kinds.Where(k => k.IsSelected).Select(k => k.Entry));
        values[Names] = IsLoaded ? ToJson(Controllers.Where(c => c.IsSelected).Select(c => c.Entry)) : ToJson(_names);
    }

    /// <summary>
    /// A pattern matching <paramref name="name"/> and nothing else. The DLL's patterns are ECMAScript
    /// regexes, so every character that is syntax there is escaped -- and only those: an escaped space,
    /// which .NET's Regex.Escape writes, is not one ECMAScript has to accept.
    /// </summary>
    internal static string ExactPattern(string name)
    {
        var pattern = new StringBuilder("^");

        foreach (var c in name)
        {
            if (@"\^$.|?*+()[]{}/".Contains(c))
                pattern.Append('\\');

            pattern.Append(c);
        }

        return pattern.Append('$').ToString();
    }

    /// <summary>The name an <see cref="ExactPattern"/> matches, or null for any other pattern.</summary>
    internal static string? FromExactPattern(string pattern)
    {
        if (pattern.Length < 2 || pattern[0] != '^' || pattern[^1] != '$')
            return null;

        var name = new StringBuilder();

        for (var i = 1; i < pattern.Length - 1; i++)
        {
            var c = pattern[i];

            if (c == '\\' && i + 1 < pattern.Length - 1 && @"\^$.|?*+()[]{}/".Contains(pattern[i + 1]))
                c = pattern[++i];
            else if (@"^$.|?*+()[]{}".Contains(c))
                return null;

            name.Append(c);
        }

        return name.ToString();
    }
}

/// <summary>
/// <c>AudioDevices</c>: which playback and recording devices the game sees. A picked device is stored by
/// its Core Audio endpoint id, which names it alone -- a friendly name is a substring rule, and can let
/// through more than the one device.
/// </summary>
public partial class InterposerAudioFilterViewModel : InterposerDeviceFilterViewModel
{
    public InterposerAudioFilterViewModel(IReadOnlyDictionary<string, string> values, Func<string, InheritedOptionValue> inherit)
        : base("AudioDevices.Enabled", [OutputKey, InputKey],
            "Audio devices",
            "For games that play through the wrong output -- a monitor with no speakers, a capture card -- or that pick up the wrong microphone.",
            "Show only the devices I pick",
            values, inherit)
    {
        Output = new AudioDirectionFilterViewModel("Playback devices", Seed(OutputKey), allowNone: false);
        Input = new AudioDirectionFilterViewModel("Recording devices", Seed(InputKey), allowNone: true);
    }

    private const string OutputKey = "AudioDevices.Output";
    private const string InputKey = "AudioDevices.Input";

    public AudioDirectionFilterViewModel Output { get; }

    public AudioDirectionFilterViewModel Input { get; }

    protected override async Task LoadAsync(IDeviceCatalog catalog)
    {
        var devices = await Task.Run(catalog.GetAudioDevices);

        Output.Fill(devices.Where(d => d.Direction == AudioDirection.Output));
        Input.Fill(devices.Where(d => d.Direction == AudioDirection.Input));
    }

    protected override void CollectSelection(Dictionary<string, string?> values)
    {
        values[OutputKey] = Output.GetValue();
        values[InputKey] = Input.GetValue();
    }
}

public enum AudioDirectionMode { All, Selected, None }

public sealed record AudioDirectionModeItem(AudioDirectionMode Mode, string Label);

/// <summary>
/// One direction of the audio filter. Not filtering it at all is stored as removing its list -- the DLL
/// filters a direction whose list is present, even empty -- which takes back a list the server set.
/// </summary>
public partial class AudioDirectionFilterViewModel : ObservableObject
{
    private readonly List<string> _entries;

    internal AudioDirectionFilterViewModel(string title, string? seed, bool allowNone)
    {
        Title = title;
        _entries = string.IsNullOrWhiteSpace(seed) ? new() : ParseEntries(seed);

        Modes = [new(AudioDirectionMode.All, "All of them"), new(AudioDirectionMode.Selected, "Only the ones I pick")];

        if (allowNone)
            Modes.Add(new(AudioDirectionMode.None, "None of them"));

        var mode = seed == null ? AudioDirectionMode.All
            : _entries.Count == 0 && allowNone ? AudioDirectionMode.None
            : AudioDirectionMode.Selected;

        _selectedMode = Modes.First(m => m.Mode == mode);
    }

    public string Title { get; }

    public ObservableCollection<AudioDirectionModeItem> Modes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelecting))]
    private AudioDirectionModeItem _selectedMode;

    public bool IsSelecting => SelectedMode.Mode == AudioDirectionMode.Selected;

    public ObservableCollection<SelectableDeviceViewModel> Devices { get; } = new();

    private bool _filled;

    internal void Fill(IEnumerable<AudioDevice> devices)
    {
        Devices.Clear();

        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in devices)
        {
            var hits = _entries.Where(e => string.Equals(e, device.Id, StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(e, device.Name, StringComparison.OrdinalIgnoreCase)).ToList();

            matched.UnionWith(hits);

            Devices.Add(new SelectableDeviceViewModel
            {
                Label = device.Name,
                Detail = device.IsDefault ? "Windows default" : null,
                Entry = device.Id,
                IsSelected = hits.Count > 0,
            });
        }

        // Devices not plugged in now, or names typed in by hand: kept, under the text the rule holds
        foreach (var entry in _entries.Where(e => !matched.Contains(e)))
        {
            Devices.Add(new SelectableDeviceViewModel
            {
                Label = entry,
                Detail = "Not connected",
                Entry = entry,
                IsSelected = true,
            });
        }

        _filled = true;
    }

    /// <summary>The stored list as JSON, "[]" for none, or null for not filtering this direction.</summary>
    internal string? GetValue() => SelectedMode.Mode switch
    {
        AudioDirectionMode.All => null,
        AudioDirectionMode.None => "[]",
        _ => JsonSerializer.Serialize(_filled ? Devices.Where(d => d.IsSelected).Select(d => d.Entry).ToList() : _entries),
    };

    private static List<string> ParseEntries(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string?>>(json)?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToList() ?? new();
        }
        catch (JsonException)
        {
            return new();
        }
    }
}
