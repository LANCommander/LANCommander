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
    public void EntryIcons_AreTintedByFamily()
    {
        var manager = RenderManager(source: MemoryFileSource.Sample().At("Games/Arena Blitz"));

        string ToneOf(string name) =>
            manager.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains(name)).QuerySelector(".lc-file-manager-icon-cell .lc-icon")!.ClassName!;

        Assert.Contains("lc-file-tone-folder", ToneOf("maps"));
        Assert.Contains("lc-file-tone-code", ToneOf("arena.exe"));
        Assert.Contains("lc-file-tone-text", ToneOf("arena.cfg"));
    }

    [Fact]
    public void EntryIcons_TintImagesAndArchives()
    {
        var media = RenderManager(source: MemoryFileSource.Sample().At("Media"));
        Assert.Contains("lc-file-tone-image", media.Find(".lc-file-manager-icon-cell .lc-icon").ClassName);

        var archives = RenderManager(source: MemoryFileSource.Sample().At("Archives"));
        Assert.All(archives.FindAll(".lc-file-manager-icon-cell .lc-icon"), icon => Assert.Contains("lc-file-tone-archive", icon.ClassName));
    }

    [Fact]
    public void Folders_ShowADashForSize()
    {
        var manager = RenderManager();

        var games = manager.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains("Games"));

        Assert.Equal("—", games.QuerySelector(".lc-file-manager-size")!.TextContent.Trim());
    }

    [Fact]
    public void UnselectableRows_AreMarked_AndTheHintCountsThem()
    {
        var manager = RenderManager(p => p
            .Add(x => x.EntrySelectable, e => e is FileManagerDirectory)
            .Add(x => x.UnselectableHint, "This picker takes a folder, so the {n} here are shown for context only."));

        var file = manager.FindAll("tbody tr.rz-data-row").First(r => r.TextContent.Contains("setup.log"));

        Assert.Equal("unselectable", file.GetAttribute("data-lc-mark"));
        Assert.Empty(file.QuerySelectorAll(".rz-chkbox"));
        Assert.Equal("This picker takes a folder, so the 1 file here are shown for context only.", manager.Find(".lc-file-manager-hint").TextContent.Trim());
    }

    [Fact]
    public void Breadcrumbs_RootIsMono_AndTheCurrentFolderIsNotALink()
    {
        var manager = RenderManager(source: MemoryFileSource.Sample().At("Games"));

        var items = manager.FindAll(".lc-breadcrumb-item");

        Assert.Contains("lc-breadcrumb-item-mono", items[0].ClassName);
        Assert.NotNull(items[0].QuerySelector("button"));
        Assert.Null(items[^1].QuerySelector("button"));
    }

    [Fact]
    public void RootName_NamesTheTopOfTheTreeAndBreadcrumbs()
    {
        var manager = RenderManager(p => p.Add(x => x.RootName, "338.4.zip"));

        Assert.Equal("338.4.zip", Breadcrumbs(manager)[0]);
        Assert.Equal("338.4.zip", manager.Find(".lc-file-manager-root").TextContent.Trim());
    }

    [Fact]
    public void Tree_TintsTheFoldersOnTheOpenPath()
    {
        var manager = RenderManager(source: MemoryFileSource.Sample().At("Games"));

        manager.Find(".lc-tree-toggle").Click();

        var icons = manager.FindAll(".lc-tree-node")
            .ToDictionary(n => n.QuerySelector(".lc-tree-label")!.TextContent.Trim(), n => n.QuerySelector(".lc-tree-icon")!.ClassName!);

        Assert.Contains("lc-file-tone-folder", icons["Games"]);
        Assert.DoesNotContain("lc-file-tone-folder", icons["Media"]);
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
