using Avalonia.Controls;
using LANCommander.Packager.Models;

namespace LANCommander.Packager.Views;

public partial class FileSelectionView : UserControl
{
    private readonly PackageContext _context;
    private CheckableTreeNode _root = new();

    public FileSelectionView(PackageContext context)
    {
        _context = context;
        InitializeComponent();

        SelectAllButton.Click += (_, _) => { _root.IsChecked = true; UpdateCount(); };
        SelectNoneButton.Click += (_, _) => { _root.IsChecked = false; UpdateCount(); };
    }

    public void PopulateFiles()
    {
        var installDir = _context.InstallDirectory;

        List<(string fullPath, string relativePath)> files;

        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
        {
            files = _context.FileChanges
                .Select(f => (fullPath: f.Path, relativePath: f.Path))
                .Where(f => File.Exists(f.fullPath))
                .DistinctBy(f => f.fullPath.ToLowerInvariant())
                .OrderBy(f => f.relativePath)
                .ToList();
        }
        else
        {
            try
            {
                files = Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories)
                    .Select(f => (fullPath: f, relativePath: Path.GetRelativePath(installDir, f)))
                    .OrderBy(f => f.relativePath)
                    .ToList();
            }
            catch
            {
                files = [];
            }
        }

        _root = CheckableTreeNode.BuildFileTree(files);
        _root.OnTreeSelectionChanged = UpdateCount;
        FileTree.ItemsSource = _root.Children;
        UpdateCount();
    }

    public void ApplySelection()
    {
        _context.SelectedFiles.Clear();
        
        foreach (var leaf in _root.GetCheckedLeaves())
        {
            if (!string.IsNullOrEmpty(leaf.FullPath))
                _context.SelectedFiles.Add(leaf.FullPath);
        }
    }

    private void UpdateCount()
    {
        var selected = _root.CountCheckedLeaves();
        var total = _root.CountTotalLeaves();
        
        CountLabel.Text = $"{selected} / {total} files selected";
    }
}
