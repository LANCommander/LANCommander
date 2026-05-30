using Avalonia.Controls;
using LANCommander.Packager.Models;

namespace LANCommander.Packager.Views;

public partial class RegistrySelectionView : UserControl
{
    private readonly PackageContext _context;
    private CheckableTreeNode _root = new();

    public RegistrySelectionView(PackageContext context)
    {
        _context = context;
        InitializeComponent();

        SelectAllButton.Click += (_, _) => { _root.IsChecked = true; UpdateCount(); };
        SelectNoneButton.Click += (_, _) => { _root.IsChecked = false; UpdateCount(); };
    }

    public void PopulateEntries()
    {
        _root = CheckableTreeNode.BuildRegistryTree(_context.RegistryChanges);
        _root.OnTreeSelectionChanged = UpdateCount;
        RegistryTree.ItemsSource = _root.Children;
        UpdateCount();
    }

    public void ApplySelection()
    {
        _context.SelectedRegistryEntries.Clear();
        
        foreach (var leaf in _root.GetCheckedLeaves())
        {
            if (leaf.SourceIndex >= 0 && leaf.SourceIndex < _context.RegistryChanges.Count)
                _context.SelectedRegistryEntries.Add(_context.RegistryChanges[leaf.SourceIndex]);
        }
    }

    private void UpdateCount()
    {
        var selected = _root.CountCheckedLeaves();
        var total = _root.CountTotalLeaves();
        
        CountLabel.Text = $"{selected} / {total} entries selected";
    }
}
