using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>A game, addon, redistributable or tool in the script tree.</summary>
public sealed class ScriptOwnerViewModel
{
    public ScriptOwnerViewModel(ScriptOwnerNode node)
    {
        Node = node;

        foreach (var entry in node.Scripts)
            Scripts.Add(new ScriptItemViewModel(entry));
    }

    public ScriptOwnerNode Node { get; }

    public string Name => Node.Name;

    public string KindLabel => Node.Kind switch
    {
        ScriptOwnerKind.Redistributable => "Redistributable",
        ScriptOwnerKind.Tool => "Tool",
        _ when Node.IsAddon => "Addon",
        _ => "Game",
    };

    public ObservableCollection<ScriptItemViewModel> Scripts { get; } = new();
}

/// <summary>One script in the tree.</summary>
public sealed partial class ScriptItemViewModel : ObservableObject
{
    public ScriptItemViewModel(ScriptEntry entry) => _entry = entry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    [NotifyPropertyChangedFor(nameof(TypeLabel))]
    [NotifyPropertyChangedFor(nameof(SourceLabel))]
    [NotifyPropertyChangedFor(nameof(IsOnServer))]
    [NotifyPropertyChangedFor(nameof(RequiresAdmin))]
    private ScriptEntry _entry;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isRunning;

    public ScriptKey Key => Entry.Key;

    public string Name => Entry.Name;

    /// <summary>The script type in words, e.g. "Before Start", shown as a badge beside the name.</summary>
    public string TypeLabel => WordBoundary().Replace(Entry.Type.ToString(), " ");

    public bool RequiresAdmin => Entry.RequiresAdmin;

    /// <summary>Only on the server: not installed and no draft. Shown as a cloud rather than a label.</summary>
    public bool IsOnServer => Entry.Source == ScriptSource.Server;

    public string SourceLabel => Entry.Source switch
    {
        ScriptSource.Installed => "installed",
        ScriptSource.Draft => "draft",
        _ => "server",
    };

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
