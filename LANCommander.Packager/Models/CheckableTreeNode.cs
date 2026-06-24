using System.Collections.ObjectModel;
using System.ComponentModel;

namespace LANCommander.Packager.Models;

public class CheckableTreeNode : INotifyPropertyChanged
{
    private bool? _isChecked = true;
    private bool _isExpanded = true;
    private bool _suppressEvents;

    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int SourceIndex { get; set; } = -1;
    public string? Indicator { get; set; }
    public bool IsCreate => Indicator == "+";
    public bool IsUpdate => Indicator == "~";
    public bool IsLeaf => Children.Count == 0;
    public ObservableCollection<CheckableTreeNode> Children { get; } = new();
    public CheckableTreeNode? Parent { get; set; }
    public Action? OnTreeSelectionChanged { get; set; }

    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            var effective = value ?? true;
            
            if (_isChecked == effective)
                return;

            _isChecked = effective;
            
            PropertyChanged?.Invoke(this, new(nameof(IsChecked)));

            if (!_suppressEvents)
            {
                SetChildrenChecked(effective);
                Parent?.RecalculateChecked();
                GetRoot().OnTreeSelectionChanged?.Invoke();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
            }
        }
    }

    private void SetChildrenChecked(bool value)
    {
        foreach (var child in Children)
        {
            child._suppressEvents = true;
            child._isChecked = value;
            child.PropertyChanged?.Invoke(child, new(nameof(IsChecked)));
            child._suppressEvents = false;
            child.SetChildrenChecked(value);
        }
    }

    private void RecalculateChecked()
    {
        if (Children.Count == 0) return;

        var allChecked = Children.All(c => c.IsChecked == true);
        var allUnchecked = Children.All(c => c.IsChecked == false);
        bool? newState = allChecked ? true : allUnchecked ? false : null;

        if (_isChecked != newState)
        {
            _suppressEvents = true;
            _isChecked = newState;
            PropertyChanged?.Invoke(this, new(nameof(IsChecked)));
            _suppressEvents = false;
            Parent?.RecalculateChecked();
        }
    }

    private CheckableTreeNode GetRoot()
    {
        var node = this;
        while (node.Parent != null) node = node.Parent;
        return node;
    }

    public IEnumerable<CheckableTreeNode> GetCheckedLeaves()
    {
        if (Children.Count == 0 && IsChecked == true)
        {
            yield return this;
            yield break;
        }

        foreach (var child in Children)
            foreach (var leaf in child.GetCheckedLeaves())
                yield return leaf;
    }

    public int CountCheckedLeaves()
    {
        if (Children.Count == 0)
            return IsChecked == true ? 1 : 0;
        
        return Children.Sum(c => c.CountCheckedLeaves());
    }

    public int CountTotalLeaves()
    {
        if (Children.Count == 0)
            return 1;
        
        return Children.Sum(c => c.CountTotalLeaves());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static CheckableTreeNode BuildFileTree(IEnumerable<(string fullPath, string relativePath)> files)
    {
        var root = new CheckableTreeNode { Name = "Root", IsExpanded = true };

        foreach (var (fullPath, relativePath) in files)
        {
            var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var current = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var existing = current.Children.FirstOrDefault(
                    c => c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                    current = existing;
                else
                {
                    var node = new CheckableTreeNode
                    {
                        Name = part,
                        Parent = current,
                        FullPath = i == parts.Length - 1 ? fullPath : "",
                        IsExpanded = i < 2
                    };
                    
                    current.Children.Add(node);
                    current = node;
                }
            }
        }

        return root;
    }

    public static CheckableTreeNode BuildRegistryTree(IList<RegistryChangeEntry> entries)
    {
        var root = new CheckableTreeNode { Name = "Registry", IsExpanded = true };

        // Deduplicate by (KeyPath, ValueName), keeping one representative entry per unique pair
        var deduped = entries
            .Select((entry, index) => (entry, index))
            .GroupBy(x => (
                key: x.entry.KeyPath.ToLowerInvariant(),
                value: x.entry.ValueName.ToLowerInvariant()))
            .Select(g =>
            {
                var isCreate = g.Any(x =>
                    x.entry.Verb.Equals("REG CREATE", StringComparison.OrdinalIgnoreCase));
                var first = g.First();
                return (entry: first.entry, index: first.index, isCreate);
            })
            .ToList();

        foreach (var (entry, index, isCreate) in deduped)
        {
            var keyParts = entry.KeyPath.Split('\\');
            var current = root;

            foreach (var part in keyParts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                var existing = current.Children.FirstOrDefault(
                    c => c.Children.Count > 0 &&
                         c.Name.Equals(part, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                    current = existing;
                else
                {
                    var node = new CheckableTreeNode
                    {
                        Name = part,
                        Parent = current,
                        IsExpanded = true
                    };
                    
                    current.Children.Add(node);
                    current = node;
                }
            }

            var valueName = string.IsNullOrEmpty(entry.ValueName) ? "(Default)" : entry.ValueName;
            var leaf = new CheckableTreeNode
            {
                Name = valueName,
                Parent = current,
                SourceIndex = index,
                Indicator = isCreate ? "+" : "~"
            };
            
            current.Children.Add(leaf);
        }

        return root;
    }
}
