using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Chooses which captured registry changes become install and uninstall scripts.
/// </summary>
public partial class RegistrySelectionStepViewModel : PackagingStepViewModel
{
    public RegistrySelectionStepViewModel(PackagingWizardViewModel wizard) : base(wizard)
    {
    }

    public override string Title => "Registry";

    public ObservableCollection<CheckableTreeNode> Roots { get; } = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>
    /// Plenty of games touch no registry at all. Showing an empty tree with a caveat about
    /// value data is noise, so the step is skipped outright.
    /// </summary>
    public override bool IsApplicable => Package.RegistryChanges.Count > 0;

    private CheckableTreeNode? _root;

    public override Task OnEnterAsync()
    {
        _root = CheckableTreeNode.BuildRegistryTree(Package.RegistryChanges);
        _root.OnTreeSelectionChanged = UpdateSummary;

        Roots.Clear();

        foreach (var child in _root.Children)
            Roots.Add(child);

        UpdateSummary();

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    private void SetAll(bool value)
    {
        foreach (var node in Roots)
            node.IsChecked = value;

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selected = Roots.Sum(r => r.CountCheckedLeaves());
        var total = Roots.Sum(r => r.CountTotalLeaves());

        // Value data is not captured by the hooks, so scripts recreate keys and values with
        // empty values. Stated inline rather than as a warning banner.
        Summary = $"{selected} of {total} registry value(s) selected. " +
                  "Values are recreated empty — fill in any that matter before publishing.";

        // Deselecting everything is valid; the scripts are simply not generated.
        CanGoNext = true;
    }

    public override Task OnLeaveAsync()
    {
        Package.SelectedRegistryEntries = Roots
            .SelectMany(r => r.GetCheckedLeaves())
            .Where(n => n.SourceIndex >= 0 && n.SourceIndex < Package.RegistryChanges.Count)
            .Select(n => Package.RegistryChanges[n.SourceIndex])
            .ToList();

        return Task.CompletedTask;
    }
}
