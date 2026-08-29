using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using LANCommander.Packaging;
using LANCommander.Packaging.Changes;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// A node in a tri-state checkbox tree, used for both file and registry selection.
/// </summary>
/// <remarks>
/// Checking a node checks its whole subtree; a parent shows indeterminate when its children
/// disagree. Recalculation is suppressed while cascading so a single click does not fan out
/// into one selection-changed notification per descendant.
/// </remarks>
public class CheckableTreeNode : INotifyPropertyChanged
{
    private bool? _isChecked = true;
    private bool _isExpanded = true;
    private bool _suppressEvents;

    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path for file leaves; empty for directories and registry nodes.</summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>Index into the source collection for registry leaves; -1 otherwise.</summary>
    public int SourceIndex { get; set; } = -1;

    /// <summary>"+" for a created key, "~" for an updated value.</summary>
    public string? Indicator { get; set; }

    public bool IsCreate => Indicator == "+";

    public bool IsUpdate => Indicator == "~";

    public bool IsLeaf => Children.Count == 0;

    public ObservableCollection<CheckableTreeNode> Children { get; } = new();

    public CheckableTreeNode? Parent { get; set; }

    /// <summary>Raised on the root whenever any node's checked state changes.</summary>
    public Action? OnTreeSelectionChanged { get; set; }

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            // A user click on an indeterminate parent means "select everything".
            var effective = value ?? true;

            if (_isChecked == effective)
                return;

            _isChecked = effective;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));

            if (_suppressEvents)
                return;

            SetChildrenChecked(effective);
            Parent?.RecalculateChecked();
            GetRoot().OnTreeSelectionChanged?.Invoke();
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            _isExpanded = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetChildrenChecked(bool value)
    {
        foreach (var child in Children)
        {
            child._suppressEvents = true;
            child._isChecked = value;
            child.PropertyChanged?.Invoke(child, new PropertyChangedEventArgs(nameof(IsChecked)));
            child._suppressEvents = false;

            child.SetChildrenChecked(value);
        }
    }

    private void RecalculateChecked()
    {
        if (Children.Count == 0)
            return;

        var allChecked = Children.All(c => c.IsChecked == true);
        var allUnchecked = Children.All(c => c.IsChecked == false);

        bool? newState = allChecked ? true : allUnchecked ? false : null;

        if (_isChecked == newState)
            return;

        _suppressEvents = true;
        _isChecked = newState;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        _suppressEvents = false;

        Parent?.RecalculateChecked();
    }

    private CheckableTreeNode GetRoot()
    {
        var node = this;

        while (node.Parent != null)
            node = node.Parent;

        return node;
    }

    public void SetAllChecked(bool value)
    {
        IsChecked = value;

        // A root with no children never fires the cascade above.
        if (Children.Count == 0)
            GetRoot().OnTreeSelectionChanged?.Invoke();
    }

    public IEnumerable<CheckableTreeNode> GetCheckedLeaves()
    {
        if (Children.Count == 0)
        {
            if (IsChecked == true)
                yield return this;

            yield break;
        }

        foreach (var child in Children)
        {
            foreach (var leaf in child.GetCheckedLeaves())
                yield return leaf;
        }
    }

    public int CountCheckedLeaves() =>
        Children.Count == 0
            ? IsChecked == true ? 1 : 0
            : Children.Sum(c => c.CountCheckedLeaves());

    public int CountTotalLeaves() =>
        Children.Count == 0 ? 1 : Children.Sum(c => c.CountTotalLeaves());

    /// <summary>
    /// Builds a directory tree from absolute paths and their paths relative to the install root.
    /// </summary>
    public static CheckableTreeNode BuildFileTree(IEnumerable<(string FullPath, string RelativePath)> files)
    {
        var root = new CheckableTreeNode { Name = "Root", IsExpanded = true };

        foreach (var (fullPath, relativePath) in files)
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = root;

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (string.IsNullOrEmpty(part))
                    continue;

                var existing = current.Children.FirstOrDefault(
                    c => c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    current = existing;

                    continue;
                }

                var node = new CheckableTreeNode
                {
                    Name = part,
                    Parent = current,
                    FullPath = i == parts.Length - 1 ? fullPath : string.Empty,
                    // Deep trees are unreadable fully expanded; the first couple of levels are
                    // enough to orient the user.
                    IsExpanded = i < 2,
                };

                current.Children.Add(node);
                current = node;
            }
        }

        return root;
    }

    /// <summary>
    /// Builds a registry tree, one leaf per captured value.
    /// </summary>
    public static CheckableTreeNode BuildRegistryTree(IList<RegistryChange> entries)
    {
        var root = new CheckableTreeNode { Name = "Registry", IsExpanded = true };

        foreach (var (entry, index) in entries.Select((e, i) => (e, i)))
        {
            var current = root;

            foreach (var part in entry.KeyPath.Split('\\'))
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                // Match only against nodes that are already keys, so a value named the same as
                // a subkey does not swallow it.
                var existing = current.Children.FirstOrDefault(
                    c => c.SourceIndex == -1 && c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    current = existing;

                    continue;
                }

                var node = new CheckableTreeNode
                {
                    Name = part,
                    Parent = current,
                    IsExpanded = true,
                };

                current.Children.Add(node);
                current = node;
            }

            var valueName = string.IsNullOrEmpty(entry.ValueName) ? "(Default)" : entry.ValueName;

            // The same key path written from a 32-bit process lands somewhere different under
            // WOW64, so both can legitimately appear. Label them so two same-named leaves under
            // one key are not just confusing.
            if (entry.SourceArchitecture == ProcessArchitecture.X86)
                valueName += "  (32-bit)";

            current.Children.Add(new CheckableTreeNode
            {
                Name = valueName,
                Parent = current,
                SourceIndex = index,
                Indicator = entry.Verb.Equals("REG CREATE", StringComparison.OrdinalIgnoreCase) ? "+" : "~",
            });
        }

        return root;
    }
}
