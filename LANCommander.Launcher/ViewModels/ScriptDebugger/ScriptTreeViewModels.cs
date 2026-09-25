using System.Collections.ObjectModel;
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
    [NotifyPropertyChangedFor(nameof(SourceLabel))]
    [NotifyPropertyChangedFor(nameof(RequiresAdmin))]
    private ScriptEntry _entry;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isRunning;

    public ScriptKey Key => Entry.Key;

    public string Name => Entry.Name == Entry.Type.ToString() ? Entry.Type.ToString() : $"{Entry.Type}: {Entry.Name}";

    public bool RequiresAdmin => Entry.RequiresAdmin;

    public string SourceLabel => Entry.Source switch
    {
        ScriptSource.Installed => "installed",
        ScriptSource.Draft => "draft",
        _ => "server",
    };
}
