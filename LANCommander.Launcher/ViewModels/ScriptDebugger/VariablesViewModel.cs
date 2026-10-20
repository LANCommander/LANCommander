using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>Locals for the selected call-stack frame.</summary>
public sealed partial class VariablesViewModel : ObservableObject
{
    private readonly DebugSessionController _controller;

    public VariablesViewModel(DebugSessionController controller) => _controller = controller;

    public ObservableCollection<VariableNodeViewModel> Nodes { get; } = new();

    [ObservableProperty]
    private string _emptyMessage = "Not stopped.";

    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>Show the variables captured for one frame. Already flattened by the engine.</summary>
    public void Show(IReadOnlyList<VariableInfo> variables)
    {
        Nodes.Clear();

        foreach (var variable in variables)
            Nodes.Add(new VariableNodeViewModel(variable, _controller));

        EmptyMessage = variables.Count == 0 ? "No variables in this scope." : string.Empty;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Clear()
    {
        Nodes.Clear();
        EmptyMessage = "Not stopped.";
        OnPropertyChanged(nameof(IsEmpty));
    }
}
