using AngleSharp.Dom;
using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The import dialog lists the package's records with a checkbox each; unchecked records should be
/// left out of the import.
/// </summary>
[Collection("BUnit")]
public class ImportUploadDialogComponentTests : BUnitTestContext
{
    public ImportUploadDialogComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    /// <summary>What a finished chunked upload leaves behind: the package stored under an object key.</summary>
    private async Task<string> StageUploadedPackageAsync(Manifest.Game manifest)
        => await StageUploadedFileAsync(await ImportPackages.WriteAsync(manifest));

    private async Task<string> StageUploadedFileAsync(string packagePath)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var storageLocation = await scope.ServiceProvider.GetRequiredService<StorageLocationService>().DefaultAsync(StorageLocationType.Archive);
        var objectKey = Guid.NewGuid().ToString();

        File.Copy(packagePath, Path.Combine(storageLocation!.Path, objectKey));

        await scope.ServiceProvider.GetRequiredService<ArchiveService>().AddAsync(new Archive
        {
            ObjectKey = objectKey,
            Version = string.Empty,
            StorageLocationId = storageLocation.Id,
        });

        return objectKey;
    }

    /// <summary>Opens the dialog the way the upload indicator does once an upload has finished.</summary>
    private IRenderedComponent<ComponentHost> OpenDialog(string objectKey, bool waitForRecords = true)
    {
        var host = Render<ComponentHost>();

        _ = Services.GetRequiredService<DialogService>().OpenAsync<ImportUploadDialog, ImportDialogOptions, bool>("Import", new ImportDialogOptions
        {
            ManifestType = ManifestType.Game,
            PreUploadedObjectKey = objectKey,
        }, new DialogSettings { NoFooter = true });

        if (waitForRecords)
            host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog .lc-tree-checkbox")), TimeSpan.FromSeconds(10));

        return host;
    }

    /// <summary>A file that isn't an import package says so and goes back to choosing one.</summary>
    [Fact]
    public async Task UnreadablePackageIsReportedAndOffersAnotherUpload()
    {
        var notAPackage = Path.Combine(ImportPackages.TempDirectory(), "not-a-package.lcx");
        await File.WriteAllTextAsync(notAPackage, "this is not a zip");

        var host = OpenDialog(await StageUploadedFileAsync(notAPackage), waitForRecords: false);

        host.WaitForAssertion(() =>
        {
            Assert.NotEmpty(host.FindAll(".lc-dialog input[type=file]"));
            Assert.Contains(Services.GetRequiredService<Radzen.NotificationService>().Messages, m => m.Summary!.Contains("could not be read"));
        }, TimeSpan.FromSeconds(10));
    }

    /// <summary>Records left out of a finished import are listed with the result, not dropped silently.</summary>
    [Fact]
    public async Task SkippedRecordsAreShownWhenTheImportCompletes()
    {
        var manifest = ImportPackages.Game();
        manifest.Saves.Single().User = $"nobody-{Guid.NewGuid():N}";

        var host = OpenDialog(await StageUploadedPackageAsync(manifest));

        ImportAndWait(host);

        host.WaitForAssertion(() => Assert.Contains("was skipped", host.Markup), TimeSpan.FromSeconds(5));
    }

    /// <summary>When the package's game itself can't be imported, the dialog says the import failed and why.</summary>
    [Fact]
    public async Task FailedImportShowsWhy()
    {
        var manifest = ImportPackages.Game();
        manifest.Title = null!;

        var host = OpenDialog(await StageUploadedPackageAsync(manifest));

        host.FindAll(".lc-dialog button").First(b => b.TextContent.Trim() == "Import").Click();

        host.WaitForAssertion(() =>
        {
            Assert.Contains("Import Failed!", host.Markup);
            Assert.NotEmpty(host.FindAll(".lc-dialog .lc-alert, .lc-dialog [role=alert]"));
        }, TimeSpan.FromSeconds(15));
    }

    /// <summary>The tree row starting with <paramref name="text"/>, once it has rendered.</summary>
    private static IElement Node(IRenderedComponent<ComponentHost> host, string text)
    {
        IElement? node = null;

        host.WaitForAssertion(() =>
        {
            node = host.FindAll(".lc-dialog .lc-tree-node").FirstOrDefault(n => n.TextContent.Trim().StartsWith(text));
            Assert.NotNull(node);
        }, TimeSpan.FromSeconds(5));

        return node!;
    }

    private static void ImportAndWait(IRenderedComponent<ComponentHost> host)
    {
        host.FindAll(".lc-dialog button").First(b => b.TextContent.Trim() == "Import").Click();

        host.WaitForAssertion(() => Assert.Contains("Import Complete!", host.Markup), TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task UncheckedActionIsNotImported()
    {
        var manifest = ImportPackages.Game();
        var host = OpenDialog(await StageUploadedPackageAsync(manifest));

        Node(host, "Action").QuerySelector(".lc-tree-toggle")!.Click();
        Node(host, "Editor").QuerySelector(".lc-tree-checkbox")!.Click();

        ImportAndWait(host);

        using var scope = Fixture.Factory.RealServices.CreateScope();
        var actions = await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id);

        Assert.Equal(["Play"], actions.Select(a => a.Name));
    }

    [Fact]
    public async Task UncheckedGroupIsNotImported()
    {
        var manifest = ImportPackages.Game();
        var host = OpenDialog(await StageUploadedPackageAsync(manifest));

        Node(host, "Save").QuerySelector(".lc-tree-checkbox")!.Click();

        ImportAndWait(host);

        using var scope = Fixture.Factory.RealServices.CreateScope();

        Assert.Empty(await scope.ServiceProvider.GetRequiredService<GameSaveService>().GetAsync(s => s.GameId == manifest.Id));
        Assert.Equal(2, (await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id)).Count);
    }
}
