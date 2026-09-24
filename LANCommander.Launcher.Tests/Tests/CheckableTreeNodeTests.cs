using System;
using System.Collections.Generic;
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
    public void AnnotatesOnlyTheLeavesItWasGiven()
    {
        // How the Customize step's findings reach the file tree: a no-CD patch has to be
        // findable among the thousands of files the installer produced.
        var root = CheckableTreeNode.BuildFileTree(
            [
                (@"C:\Games\Example\game.exe", @"game.exe"),
                (@"C:\Games\Example\ddraw.dll", @"ddraw.dll"),
                (@"C:\Games\Example\Data\a.dat", @"Data\a.dat"),
            ],
            // Keyed case-insensitively: the capture and the folder scan do not always agree on
            // the casing of a path the OS considers the same file.
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [@"c:\games\example\game.exe"] = "changed",
                [@"C:\Games\Example\ddraw.dll"] = "added",
            });

        var byName = root.Children.ToDictionary(c => c.Name);

        Assert.Equal("changed", byName["game.exe"].Annotation);
        Assert.Equal("added", byName["ddraw.dll"].Annotation);

        // Directories are not leaves, so they carry no badge even when their contents changed.
        Assert.False(byName["Data"].HasAnnotation);
        Assert.False(byName["Data"].Children[0].HasAnnotation);
    }

    [Fact]
    public void RollsFileSizesUpIntoDirectories()
    {
        var sizes = new Dictionary<string, long>
        {
            [@"C:\Games\Example\game.exe"] = 1000,
            [@"C:\Games\Example\Data\a.dat"] = 200,
            [@"C:\Games\Example\Data\b.dat"] = 30,
        };

        var root = CheckableTreeNode.BuildFileTree(
            sizes.Keys.Select(p => (p, p.Substring(@"C:\Games\Example\".Length))),
            sizeOf: p => sizes[p]);

        var data = root.Children.First(c => c.Name == "Data");

        Assert.Equal(230, data.Size);
        Assert.Equal(1230, root.Size);

        // The summary's byte total follows the selection, not the tree.
        data.Children[0].IsChecked = false;

        Assert.Equal(1030, root.SumCheckedSize());
        Assert.True(data.Children[0].IsExcluded);
    }

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
