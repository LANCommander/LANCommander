using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.SDK.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// ViewModel backing the launcher's per-game options overlay. Builds a tree of option nodes from a
/// game's YAML <see cref="OptionSchema"/>, seeded with the user's locally-stored values (falling back to
/// schema defaults), and serializes the edited values back into a dot-notation dictionary on save.
/// </summary>
public partial class GameOptionsOverlayViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    /// <summary>Top-level options that are not grouped. Rendered above the tab strip (or as the whole body when there are no tabs).</summary>
    [ObservableProperty]
    private ObservableCollection<GameOptionNodeViewModel> _rootLeaves = new();

    /// <summary>Top-level option groups. Rendered as horizontal tabs.</summary>
    [ObservableProperty]
    private ObservableCollection<GameOptionGroupViewModel> _tabs = new();

    public bool HasRootLeaves => RootLeaves.Count > 0;
    public bool HasTabs => Tabs.Count > 0;

    /// <summary>
    /// Builds the view-model tree. Returns null when the schema is empty or fails to parse, in which case
    /// there is nothing for the user to configure.
    /// </summary>
    public static GameOptionsOverlayViewModel? Build(string? optionSchemaYaml, string? optionsJson, string gameTitle)
    {
        if (string.IsNullOrWhiteSpace(optionSchemaYaml))
            return null;

        OptionSchema schema;

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(new OptionChoiceYamlConverter())
                .IgnoreUnmatchedProperties()
                .Build();

            schema = deserializer.Deserialize<OptionSchema>(optionSchemaYaml);
        }
        catch
        {
            return null;
        }

        if (schema?.Options == null || schema.Options.Count == 0)
            return null;

        var values = ParseOptionsJson(optionsJson);

        var vm = new GameOptionsOverlayViewModel
        {
            DialogTitle = $"{gameTitle} Options",
        };

        foreach (var node in BuildNodes(schema.Options, string.Empty, values))
        {
            if (node is GameOptionGroupViewModel group)
                vm.Tabs.Add(group);
            else
                vm.RootLeaves.Add(node);
        }

        return vm;
    }

    /// <summary>
    /// Walks the tree and collects every leaf/list value into a flat dot-notation dictionary, matching the
    /// storage convention used by the server form and the <c>Get-GameOptions</c> cmdlet.
    /// </summary>
    public Dictionary<string, string> CollectValues()
    {
        var result = new Dictionary<string, string>();

        foreach (var node in RootLeaves)
            node.CollectInto(result);

        foreach (var tab in Tabs)
            tab.CollectInto(result);

        return result;
    }

    private static Dictionary<string, string> ParseOptionsJson(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return new();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(optionsJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static IEnumerable<GameOptionNodeViewModel> BuildNodes(
        Dictionary<string, OptionDefinition> options,
        string prefix,
        Dictionary<string, string> values)
    {
        foreach (var kvp in options)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
            var definition = kvp.Value;
            var label = ResolveLabel(definition, kvp.Key);

            var isGroup = string.IsNullOrWhiteSpace(definition.Type)
                          && !definition.IsList
                          && definition.Options != null
                          && definition.Options.Count > 0;

            if (isGroup)
            {
                var group = new GameOptionGroupViewModel { DisplayName = label };

                foreach (var child in BuildNodes(definition.Options!, key, values))
                    group.Children.Add(child);

                yield return group;
            }
            else if (!string.IsNullOrWhiteSpace(definition.Type))
            {
                var seed = values.TryGetValue(key, out var stored) ? stored : definition.GetDefaultAsString();

                if (definition.IsList)
                    yield return new GameOptionListViewModel(definition, key, label, seed);
                else
                    yield return CreateLeaf(definition, key, label, definition.Description, seed);
            }
        }
    }

    internal static GameOptionLeafViewModel CreateLeaf(
        OptionDefinition definition,
        string key,
        string label,
        string? description,
        string? seed)
    {
        switch ((definition.Type ?? "string").ToLowerInvariant())
        {
            case "bool":
                return new BoolOptionViewModel
                {
                    Key = key,
                    DisplayName = label,
                    Description = description,
                    Value = string.Equals(seed, "true", StringComparison.OrdinalIgnoreCase),
                };

            case "enum":
            case "choice":
                var choices = definition.Choices ?? new List<OptionChoice>();
                return new ChoiceOptionViewModel
                {
                    Key = key,
                    DisplayName = label,
                    Description = description,
                    Choices = new ObservableCollection<OptionChoice>(choices),
                    SelectedChoice = choices.FirstOrDefault(c => c.Value == seed),
                };

            case "int":
                decimal? intValue = null;
                if (decimal.TryParse(seed, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    intValue = parsed;
                return new IntOptionViewModel
                {
                    Key = key,
                    DisplayName = label,
                    Description = description,
                    Value = intValue,
                };

            default:
                return new StringOptionViewModel
                {
                    Key = key,
                    DisplayName = label,
                    Description = description,
                    Value = seed ?? string.Empty,
                };
        }
    }

    private static string ResolveLabel(OptionDefinition definition, string fallbackKey) =>
        !string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.DisplayName
        : !string.IsNullOrWhiteSpace(definition.Description) ? definition.Description
        : fallbackKey;
}

// ── Node hierarchy ──────────────────────────────────────────────────────────────────────────────────

public abstract partial class GameOptionNodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _description;

    public bool HasLabel => !string.IsNullOrWhiteSpace(DisplayName);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>Adds this node's resolved value(s) to the flat dot-notation dictionary.</summary>
    public abstract void CollectInto(Dictionary<string, string> values);
}

public partial class GameOptionGroupViewModel : GameOptionNodeViewModel
{
    [ObservableProperty]
    private ObservableCollection<GameOptionNodeViewModel> _children = new();

    public override void CollectInto(Dictionary<string, string> values)
    {
        foreach (var child in Children)
            child.CollectInto(values);
    }
}

public abstract class GameOptionLeafViewModel : GameOptionNodeViewModel
{
    public string Key { get; set; } = string.Empty;

    /// <summary>The current value of this leaf in its canonical string form.</summary>
    public abstract string GetValue();

    public override void CollectInto(Dictionary<string, string> values) => values[Key] = GetValue();
}

public partial class BoolOptionViewModel : GameOptionLeafViewModel
{
    [ObservableProperty]
    private bool _value;

    public override string GetValue() => Value ? "true" : "false";
}

public partial class ChoiceOptionViewModel : GameOptionLeafViewModel
{
    [ObservableProperty]
    private ObservableCollection<OptionChoice> _choices = new();

    [ObservableProperty]
    private OptionChoice? _selectedChoice;

    public override string GetValue() => SelectedChoice?.Value ?? string.Empty;
}

public partial class IntOptionViewModel : GameOptionLeafViewModel
{
    [ObservableProperty]
    private decimal? _value;

    public override string GetValue() =>
        Value.HasValue ? ((long)Value.Value).ToString(CultureInfo.InvariantCulture) : string.Empty;
}

public partial class StringOptionViewModel : GameOptionLeafViewModel
{
    [ObservableProperty]
    private string _value = string.Empty;

    public override string GetValue() => Value ?? string.Empty;
}

/// <summary>
/// A list option: either a scalar list (single item type) or a composite list (multiple named fields).
/// Rows are edited live; on collect, rows are serialized to a JSON array of scalars or objects.
/// </summary>
public partial class GameOptionListViewModel : GameOptionLeafViewModel
{
    private const string ScalarFieldKey = "__value";

    private readonly OptionDefinition _definition;

    public bool IsCompositeList => _definition.IsCompositeList;

    [ObservableProperty]
    private ObservableCollection<GameOptionListRowViewModel> _rows = new();

    public bool CanAdd => !_definition.MaxItems.HasValue || Rows.Count < _definition.MaxItems.Value;
    public bool CanRemoveRows => Rows.Count > (_definition.MinItems ?? 0);

    public string? BoundsHint
    {
        get
        {
            if (!_definition.MinItems.HasValue && !_definition.MaxItems.HasValue)
                return null;

            var min = _definition.MinItems.HasValue ? $"min {_definition.MinItems.Value}" : string.Empty;
            var max = _definition.MaxItems.HasValue ? $"max {_definition.MaxItems.Value}" : string.Empty;
            var sep = _definition.MinItems.HasValue && _definition.MaxItems.HasValue ? ", " : string.Empty;

            return $"{min}{sep}{max}";
        }
    }

    public bool HasBoundsHint => !string.IsNullOrEmpty(BoundsHint);

    public GameOptionListViewModel(OptionDefinition definition, string key, string label, string? seed)
    {
        _definition = definition;
        Key = key;
        DisplayName = label;
        Description = definition.Description;

        foreach (var row in ParseRows(seed))
            Rows.Add(row);
    }

    [RelayCommand]
    private void AddRow()
    {
        if (!CanAdd)
            return;

        Rows.Add(BuildRow(rowValues: null));
        RaiseRowStateChanged();
    }

    public void RemoveRow(GameOptionListRowViewModel row)
    {
        if (!CanRemoveRows)
            return;

        Rows.Remove(row);
        RaiseRowStateChanged();
    }

    private void RaiseRowStateChanged()
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanRemoveRows));

        foreach (var row in Rows)
            row.NotifyCanRemoveChanged();
    }

    private IEnumerable<GameOptionListRowViewModel> ParseRows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            yield break;

        JsonElement root;

        using (var doc = TryParse(json, out var ok))
        {
            if (!ok || doc!.RootElement.ValueKind != JsonValueKind.Array)
                yield break;

            root = doc.RootElement.Clone();
        }

        foreach (var item in root.EnumerateArray())
        {
            if (IsCompositeList)
            {
                var rowValues = new Dictionary<string, string>();

                if (item.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in _definition.Fields!)
                    {
                        if (item.TryGetProperty(field.Key, out var fieldEl))
                            rowValues[field.Key] = ReadScalar(fieldEl);
                    }
                }

                yield return BuildRow(rowValues);
            }
            else
            {
                yield return BuildRow(new Dictionary<string, string> { [ScalarFieldKey] = ReadScalar(item) });
            }
        }
    }

    private GameOptionListRowViewModel BuildRow(Dictionary<string, string>? rowValues)
    {
        var row = new GameOptionListRowViewModel(this);

        if (IsCompositeList)
        {
            foreach (var field in _definition.Fields!)
            {
                var fieldLabel = !string.IsNullOrWhiteSpace(field.Value.DisplayName) ? field.Value.DisplayName : field.Key;
                var seed = rowValues != null && rowValues.TryGetValue(field.Key, out var v)
                    ? v
                    : field.Value.GetDefaultAsString();

                row.Fields.Add(GameOptionsOverlayViewModel.CreateLeaf(field.Value, field.Key, fieldLabel, field.Value.Description, seed));
            }
        }
        else
        {
            var itemDefinition = new OptionDefinition
            {
                Type = string.IsNullOrWhiteSpace(_definition.ItemType) ? "string" : _definition.ItemType,
            };

            var seed = rowValues != null && rowValues.TryGetValue(ScalarFieldKey, out var v) ? v : null;

            // No display name -> the row renders the input only, with no label.
            row.Fields.Add(GameOptionsOverlayViewModel.CreateLeaf(itemDefinition, ScalarFieldKey, string.Empty, null, seed));
        }

        return row;
    }

    public override string GetValue()
    {
        if (IsCompositeList)
        {
            var items = new List<Dictionary<string, string>>();

            foreach (var row in Rows)
            {
                var entry = new Dictionary<string, string>();

                foreach (var field in row.Fields)
                    entry[field.Key] = field.GetValue();

                items.Add(entry);
            }

            return JsonSerializer.Serialize(items);
        }

        var scalars = Rows
            .Select(r => r.Fields.FirstOrDefault()?.GetValue() ?? string.Empty)
            .ToList();

        return JsonSerializer.Serialize(scalars);
    }

    private static JsonDocument? TryParse(string json, out bool ok)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            ok = true;
            return doc;
        }
        catch
        {
            ok = false;
            return null;
        }
    }

    private static string ReadScalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => element.GetRawText(),
    };
}

public partial class GameOptionListRowViewModel : ViewModelBase
{
    private readonly GameOptionListViewModel _parent;

    [ObservableProperty]
    private ObservableCollection<GameOptionLeafViewModel> _fields = new();

    public bool CanRemove => _parent.CanRemoveRows;

    public GameOptionListRowViewModel(GameOptionListViewModel parent)
    {
        _parent = parent;
    }

    internal void NotifyCanRemoveChanged() => OnPropertyChanged(nameof(CanRemove));

    [RelayCommand]
    private void Remove() => _parent.RemoveRow(this);
}
