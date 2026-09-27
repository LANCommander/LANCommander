using Bunit;
using LANCommander.Server.UI.Components;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Fixtures.Components.Helpers;

namespace LANCommander.Server.UI.Tests.Controls;

public class FileManagerTests : ControlsTestContext
{
    private IRenderedComponent<FileManager> RenderManager(Action<ComponentParameterCollectionBuilder<FileManager>>? parameters = null, MemoryFileSource? source = null)
    {
        var manager = Render<FileManager>(p =>
        {
            p.Add(x => x.Source, source ?? MemoryFileSource.Sample());

            parameters?.Invoke(p);
        });

        manager.WaitForAssertion(() => Assert.NotEmpty(manager.FindAll("tbody tr.rz-data-row")));

        return manager;
    }

    private static List<string> Names(IRenderedComponent<FileManager> manager) =>
        manager.FindAll("tbody tr.rz-data-row").Select(r => r.QuerySelectorAll("td")[2].TextContent.Trim()).ToList();

    private static List<string> Breadcrumbs(IRenderedComponent<FileManager> manager) =>
        manager.FindAll(".lc-breadcrumb-item").Select(b => b.TextContent.Trim()).ToList();

    [Fact]
    public void ListsTheCurrentFolder_FoldersAndFilesByName()
    {
        var manager = RenderManager();

        Assert.Equal(["Archives", "Backups", "Games", "Media", "setup.log"], Names(manager));
    }

    [Fact]
    public void DoubleClickingAFolder_OpensIt_AndBackReturns()
    {
        var manager = RenderManager();

        manager.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains("Games")).QuerySelectorAll("td")[2].DoubleClick();

        manager.WaitForAssertion(() => Assert.Equal(["Arena Blitz", "Neon Drift"], Names(manager)));
        Assert.Equal(["Storage", "Games"], Breadcrumbs(manager));

        manager.Find("button[aria-label=Back]").Click();

        manager.WaitForAssertion(() => Assert.Contains("setup.log", Names(manager)));
        Assert.False(manager.Find("button[aria-label=Forward]").HasAttribute("disabled"));
    }

    [Fact]
    public void ClickingATreeFolder_OpensIt()
    {
        var manager = RenderManager();

        manager.Find(".lc-tree-toggle").Click();
        manager.FindAll(".lc-tree-label").First(l => l.TextContent.Trim() == "Media").ParentElement!.Click();

        manager.WaitForAssertion(() => Assert.Equal(["arena-blitz-cover.png"], Names(manager)));
    }

    [Fact]
    public void EntrySelectable_OnlyOffersMatchingRows()
    {
        var manager = RenderManager(p => p.Add(x => x.EntrySelectable, e => e is FileManagerFile), MemoryFileSource.Sample());

        var rows = manager.FindAll("tbody tr.rz-data-row");

        Assert.Empty(rows.First(r => r.TextContent.Contains("Games")).QuerySelectorAll(".rz-chkbox"));
        Assert.Single(rows.First(r => r.TextContent.Contains("setup.log")).QuerySelectorAll(".rz-chkbox"));
    }

    [Fact]
    public void SingleSelection_ReplacesThePreviousChoice()
    {
        IEnumerable<IFileManagerEntry> selected = [];

        var manager = RenderManager(
            p => p.Add(x => x.SelectMultiple, false).Add(x => x.SelectedChanged, s => selected = s),
            MemoryFileSource.Sample().At("Archives"));

        manager.FindAll("tbody .lc-table-select .rz-chkbox-box")[0].Click();
        manager.FindAll("tbody .lc-table-select .rz-chkbox-box")[1].Click();

        Assert.Equal(["neon-drift-2.0.zip"], selected.Select(s => s.Name));
        Assert.Empty(manager.FindAll("thead .rz-chkbox"));
    }

    [Fact]
    public void EntryVisible_HidesEntries()
    {
        var manager = RenderManager(p => p.Add(x => x.EntryVisible, e => e is FileManagerDirectory));

        Assert.DoesNotContain("setup.log", Names(manager));
    }

    [Fact]
    public void Features_PickTheToolbarButtons()
    {
        var manager = RenderManager(p => p.Add(x => x.Features, FileManagerFeatures.Refresh));

        Assert.Single(manager.FindAll("button[aria-label=Refresh]"));
        Assert.Empty(manager.FindAll("button[aria-label=Back]"));
        Assert.Empty(manager.FindAll("button[aria-label=Delete]"));
    }

    [Fact]
    public void Deleting_RemovesTheSelectedEntries()
    {
        var manager = RenderManager(source: MemoryFileSource.Sample().At("Games/Neon Drift"));

        manager.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains("readme.txt")).QuerySelector(".rz-chkbox-box")!.Click();
        manager.Find("button[aria-label=Delete]").Click();
        manager.WaitForAssertion(() => manager.FindAll(".lc-popconfirm button").First(b => b.TextContent.Contains("Delete")).Click());

        manager.WaitForAssertion(() => Assert.Equal(["drift.exe"], Names(manager)));
    }
}
