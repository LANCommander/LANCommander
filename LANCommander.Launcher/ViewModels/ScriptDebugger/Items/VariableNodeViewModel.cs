using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.SDK.PowerShell.Debugging;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger.Items;

/// <summary>
/// One row in the Variables tree. Children are fetched on first expansion rather than at capture
/// time, because reading a property can run a user-defined ScriptProperty getter.
/// </summary>
public sealed partial class VariableNodeViewModel : ObservableObject
{
    private static readonly VariableNodeViewModel LoadingPlaceholder = new()
    {
        Name = "...",
        TypeName = string.Empty,
        Value = "(loading)",
    };

    private readonly DebugSessionController? _controller;
    private readonly int? _handle;
    private bool _childrenLoaded;

    private VariableNodeViewModel()
    {
        Name = string.Empty;
        TypeName = string.Empty;
        Value = string.Empty;
    }

    public VariableNodeViewModel(VariableInfo info, DebugSessionController? controller)
    {
        _controller = controller;
        _handle = info.Handle;

        Name = info.Name;
        TypeName = info.TypeName;
        Value = info.Value;
        HasChildren = info.HasChildren;

        // A placeholder child is what makes the TreeView draw an expander before we know whether
        // there is anything under it.
        if (HasChildren)
            Children.Add(LoadingPlaceholder);
    }

    public string Name { get; init; }

    public string TypeName { get; init; }

    public string Value { get; init; }

    public bool HasChildren { get; }

    public ObservableCollection<VariableNodeViewModel> Children { get; } = new();

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value || _childrenLoaded || !HasChildren || _handle is null || _controller is null)
            return;

        _childrenLoaded = true;
        _ = LoadChildrenAsync(_handle.Value);
    }

    private async Task LoadChildrenAsync(int handle)
    {
        // Awaited, never blocked: the work runs on the pipeline thread through the pump, and the
        // script may resume out from under us, which surfaces as an empty result rather than a hang.
        var children = await _controller!.ExpandVariableAsync(handle);

        Children.Clear();

        foreach (var child in children)
            Children.Add(new VariableNodeViewModel(child, _controller));

        if (Children.Count == 0)
        {
            Children.Add(new VariableNodeViewModel
            {
                Name = string.Empty,
                TypeName = string.Empty,
                Value = "(no members)",
            });
        }
    }
}
