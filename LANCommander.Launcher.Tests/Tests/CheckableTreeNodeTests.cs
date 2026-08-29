using System.Linq;
using LANCommander.Launcher.ViewModels.Components;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Selection behaviour for the file and registry trees.
/// </summary>
public class CheckableTreeNodeTests
{
    private static CheckableTreeNode BuildTree() =>
        CheckableTreeNode.BuildFileTree(
        [
            (@"C:\Games\Example\game.exe", @"game.exe"),
            (@"C:\Games\Example\Data\a.dat", @"Data\a.dat"),
            (@"C:\Games\Example\Data\b.dat", @"Data\b.dat"),
        ]);

    [Fact]
    public void UncheckingALeafDeselectsIt()
    {
        // The bug behind "non-selected items show up in Launch Action": the control cycled to
        // indeterminate, the setter coerced null back to true, and the file stayed selected.
        var root = BuildTree();
        var leaf = root.Children.First(c => c.Name == "game.exe");

        leaf.IsChecked = false;

        Assert.False(leaf.IsChecked);
        Assert.DoesNotContain(root.GetCheckedLeaves(), n => n.Name == "game.exe");
    }

    [Fact]
    public void UncheckingAFolderDeselectsEveryChild()
    {
        var root = BuildTree();
        var folder = root.Children.First(c => c.Name == "Data");

        folder.IsChecked = false;

        Assert.All(folder.Children, c => Assert.False(c.IsChecked));
        Assert.DoesNotContain(root.GetCheckedLeaves(), n => n.FullPath.Contains(@"\Data\"));
    }

    [Fact]
    public void AFolderGoesIndeterminateWhenChildrenDisagree()
    {
        var root = BuildTree();
        var folder = root.Children.First(c => c.Name == "Data");

        folder.Children[0].IsChecked = false;

        Assert.Null(folder.IsChecked);
    }

    [Fact]
    public void CheckingAnIndeterminateFolderSelectsEverythingUnderIt()
    {
        var root = BuildTree();
        var folder = root.Children.First(c => c.Name == "Data");

        folder.Children[0].IsChecked = false;
        Assert.Null(folder.IsChecked);

        folder.IsChecked = true;

        Assert.All(folder.Children, c => Assert.True(c.IsChecked));
    }

    [Fact]
    public void CheckedLeafCountTracksSelection()
    {
        var root = BuildTree();

        Assert.Equal(3, root.CountCheckedLeaves());
        Assert.Equal(3, root.CountTotalLeaves());

        root.Children.First(c => c.Name == "game.exe").IsChecked = false;

        Assert.Equal(2, root.CountCheckedLeaves());
        Assert.Equal(3, root.CountTotalLeaves());
    }

    [Fact]
    public void OnlyLeavesWithPathsAreReturnedAsSelections()
    {
        var root = BuildTree();

        var leaves = root.GetCheckedLeaves().ToList();

        Assert.Equal(3, leaves.Count);
        Assert.All(leaves, l => Assert.NotEmpty(l.FullPath));
    }
}
