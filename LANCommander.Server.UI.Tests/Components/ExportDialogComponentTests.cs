using System.Reflection;
using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Components;
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

    [Fact]
    public async Task AllArchivesStayCheckedForRedistributable()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3);

        var cut = Render<ExportDialog>(parameters => parameters
            .Add(p => p.Options, new ExportDialogOptions
            {
                RecordId = redistributableId,
                RecordType = ImportExportRecordType.Redistributable,
            }));

        var selectedKeys = (string[]?)typeof(ExportDialog)
            .GetField("_selectedKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance);

        var checkboxes = cut.FindAll(".ant-tree-checkbox");
        var titles = cut.FindAll(".ant-tree-title");

        System.Console.WriteLine($"checkboxes={checkboxes.Count} titles={titles.Count}");
        System.Console.WriteLine(cut.Markup.Length > 4000 ? cut.Markup.Substring(0, 4000) : cut.Markup);
        System.Console.WriteLine("keys=" + string.Join(",", selectedKeys ?? System.Array.Empty<string>()));

        Assert.NotNull(selectedKeys);

        var parsed = selectedKeys!.Where(k => Guid.TryParse(k, out _)).ToArray();

        Assert.Equal(3, parsed.Length);
    }

    [Fact]
    public async Task AllArchivesStayCheckedWhenGroupsAreExpanded()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3, 2);

        var cut = Render<ExportDialog>(parameters => parameters
            .Add(p => p.Options, new ExportDialogOptions
            {
                RecordId = redistributableId,
                RecordType = ImportExportRecordType.Redistributable,
            }));

        foreach (var switcher in cut.FindAll(".ant-tree-switcher").ToList())
            switcher.Click();

        var selectedKeys = (string[]?)typeof(ExportDialog)
            .GetField("_selectedKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance);

        System.Console.WriteLine("expanded keys=" + string.Join(",", selectedKeys ?? System.Array.Empty<string>()));
        System.Console.WriteLine("checkboxes=" + cut.FindAll(".ant-tree-checkbox").Count);
        System.Console.WriteLine("checked=" + cut.FindAll(".ant-tree-checkbox-checked").Count);

        var parsed = selectedKeys!.Where(k => Guid.TryParse(k, out _)).ToArray();

        Assert.Equal(5, parsed.Length);
    }

    [Fact]
    public async Task UncheckingAndRecheckingAnArchiveKeepsTheRest()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3, 2);

        var cut = Render<ExportDialog>(parameters => parameters
            .Add(p => p.Options, new ExportDialogOptions
            {
                RecordId = redistributableId,
                RecordType = ImportExportRecordType.Redistributable,
            }));

        foreach (var switcher in cut.FindAll(".ant-tree-switcher").ToList())
            switcher.Click();

        string[]? Keys() => (string[]?)typeof(ExportDialog)
            .GetField("_selectedKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance);

        var checkboxes = cut.FindAll(".ant-tree-checkbox").ToList();

        // Index 0 is the "Archive" group node; 1..3 are the individual archives.
        checkboxes[1].Click();
        System.Console.WriteLine("after uncheck: " + Keys()!.Count(k => Guid.TryParse(k, out _)));

        cut.FindAll(".ant-tree-checkbox")[1].Click();
        System.Console.WriteLine("after recheck: " + Keys()!.Count(k => Guid.TryParse(k, out _)));

        Assert.Equal(5, Keys()!.Count(k => Guid.TryParse(k, out _)));
    }

    [Fact]
    public async Task UncheckingAndRecheckingTheArchiveGroupKeepsEveryArchive()
    {
        var redistributableId = await AddRedistributableWithArchivesAsync(3, 2);

        var cut = Render<ExportDialog>(parameters => parameters
            .Add(p => p.Options, new ExportDialogOptions
            {
                RecordId = redistributableId,
                RecordType = ImportExportRecordType.Redistributable,
            }));

        foreach (var switcher in cut.FindAll(".ant-tree-switcher").ToList())
            switcher.Click();

        string[]? Keys() => (string[]?)typeof(ExportDialog)
            .GetField("_selectedKeys", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance);

        cut.FindAll(".ant-tree-checkbox")[0].Click();
        System.Console.WriteLine("group uncheck: " + Keys()!.Count(k => Guid.TryParse(k, out _)));

        cut.FindAll(".ant-tree-checkbox")[0].Click();
        System.Console.WriteLine("group recheck: " + Keys()!.Count(k => Guid.TryParse(k, out _)));

        Assert.Equal(5, Keys()!.Count(k => Guid.TryParse(k, out _)));
    }
}
