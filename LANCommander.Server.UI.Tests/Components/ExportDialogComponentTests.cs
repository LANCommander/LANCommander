using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit coverage for the export dialog's selection tree. The report behind these tests is that
/// exporting a redistributable with several archives only produces one archive in the .lcx.
/// </summary>
[Collection("BUnit")]
public class ExportDialogComponentTests : BUnitTestContext
{
    public ExportDialogComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private async Task<Guid> AddRedistributableWithArchivesAsync(int archiveCount, int scriptCount = 0)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var redistributableService = scope.ServiceProvider.GetRequiredService<RedistributableService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();
        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"Redist {Guid.NewGuid():N}",
        });

        for (var i = 0; i < archiveCount; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                RedistributableId = redistributable.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"1.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await File.WriteAllTextAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"archive-{i}");
        }

        var scriptService = scope.ServiceProvider.GetRequiredService<ScriptService>();

        for (var i = 0; i < scriptCount; i++)
            await scriptService.AddAsync(new Script
            {
                RedistributableId = redistributable.Id,
                Name = $"Script {i}",
                Contents = "Write-Host 'hi'",
                Type = ScriptType.Install,
            });

        return redistributable.Id;
    }

    private IRenderedComponent<ExportDialog> RenderDialog(Guid redistributableId)
    {
        var cut = Render<ExportDialog>(parameters => parameters
            .Add(p => p.Options, new ExportDialogOptions
            {
                RecordId = redistributableId,
                RecordType = ImportExportRecordType.Redistributable,
            }));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".lc-tree-checkbox")));

        return cut;
    }

    private static void ExpandAll(IRenderedComponent<ExportDialog> cut)
    {
        // Expanding re-renders the tree, so look the toggles up again each time
        while (cut.FindAll(".lc-tree-toggle[aria-label=Expand]").FirstOrDefault() is { } toggle)
            toggle.Click();
    }

    [Fact]
    public async Task AllArchivesStayCheckedForRedistributable()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3);

        var cut = RenderDialog(redistributableId);

        Assert.Equal(3, cut.Instance.SelectedIds.Count);
    }

    [Fact]
    public async Task AllArchivesStayCheckedWhenGroupsAreExpanded()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3, 2);

        var cut = RenderDialog(redistributableId);

        ExpandAll(cut);

        Assert.Equal(5, cut.Instance.SelectedIds.Count);
    }

    [Fact]
    public async Task UncheckingAndRecheckingAnArchiveKeepsTheRest()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3, 2);

        var cut = RenderDialog(redistributableId);

        ExpandAll(cut);

        // The first archive in the group of three
        AngleSharp.Dom.IElement Archive() => cut.FindAll(".lc-tree-item[aria-level='1']")
            .First(i => i.QuerySelector(".lc-tree-node")!.TextContent.Contains("(3)"))
            .QuerySelector(".lc-tree-children .lc-tree-checkbox")!;

        Archive().Click();
        Assert.Equal(4, cut.Instance.SelectedIds.Count);

        Archive().Click();
        Assert.Equal(5, cut.Instance.SelectedIds.Count);
    }

    [Fact]
    public async Task UncheckingAndRecheckingTheArchiveGroupKeepsEveryArchive()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3, 2);

        var cut = RenderDialog(redistributableId);

        ExpandAll(cut);

        AngleSharp.Dom.IElement Group() => cut.FindAll(".lc-tree-item[aria-level='1']")
            .First(i => i.QuerySelector(".lc-tree-node")!.TextContent.Contains("(3)"))
            .QuerySelector(".lc-tree-checkbox")!;

        Group().Click();
        Assert.Equal(2, cut.Instance.SelectedIds.Count);

        Group().Click();
        Assert.Equal(5, cut.Instance.SelectedIds.Count);
    }
}
