using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// An .lcx upload finishing, as the browser's upload manager reports it, should lead to choosing
/// records: in the dialog that started it, or from the upload indicator once that dialog is gone.
/// </summary>
[Collection("BUnit")]
public class ImportUploadCompletionTests : BUnitTestContext
{
    public ImportUploadCompletionTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    /// <summary>What a finished chunked upload leaves behind: the package stored under an object key.</summary>
    private async Task<string> StageUploadedPackageAsync()
    {
        var packagePath = await ImportPackages.WriteAsync(ImportPackages.Game());

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

    private bool ImportReadyNotified() =>
        Services.GetRequiredService<Radzen.NotificationService>().Messages.Any(m => m.Summary == "Import Ready");

    [Fact]
    public async Task UploadFinishingInTheOpenDialogShowsTheRecords()
    {
        var objectKey = await StageUploadedPackageAsync();
        var host = Render<ComponentHost>();
        Render<UploadIndicator>();

        _ = Services.GetRequiredService<DialogService>().OpenAsync<ImportUploadDialog, ImportDialogOptions, bool>("Import Game", new ImportDialogOptions
        {
            ManifestType = ManifestType.Game,
        }, new DialogSettings { NoFooter = true });

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindComponents<InputFile>()));
        host.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("x", "game.lcx"));

        host.WaitForAssertion(() => Assert.False(UploadButton(host).HasAttribute("disabled")));
        UploadButton(host).Click();

        var tracker = Services.GetRequiredService<UploadTracker>();
        host.WaitForAssertion(() => Assert.NotEmpty(tracker.ActiveUploads));

        await host.InvokeAsync(() => tracker.JSOnUploadComplete(tracker.ActiveUploads.Keys.Single(), objectKey));

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog .lc-tree-checkbox")), TimeSpan.FromSeconds(10));

        // The dialog has the upload; the indicator shouldn't offer to import it a second time
        Assert.Empty(tracker.ActiveUploads);
        Assert.False(ImportReadyNotified());
    }

    [Fact]
    public async Task BackgroundUploadContinuesFromTheNotification()
    {
        var objectKey = await StageUploadedPackageAsync();
        var host = Render<ComponentHost>();
        Render<UploadIndicator>();

        var tracker = Services.GetRequiredService<UploadTracker>();
        tracker.ActiveUploads["upload"] = new BackgroundUploadInfo { UploadId = "upload", FileName = "game.lcx", Type = UploadType.Import };

        await host.InvokeAsync(() => tracker.JSOnUploadComplete("upload", objectKey));

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll("button.lc-notification-action")));
        host.FindAll("button.lc-notification-action").First().Click();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog .lc-tree-checkbox")), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task MissingUploadIsReportedInsteadOfBreakingTheDialog()
    {
        var host = Render<ComponentHost>();

        _ = Services.GetRequiredService<DialogService>().OpenAsync<ImportUploadDialog, ImportDialogOptions, bool>("Import", new ImportDialogOptions
        {
            PreUploadedObjectKey = Guid.NewGuid().ToString(),
        }, new DialogSettings { NoFooter = true });

        host.WaitForAssertion(() =>
        {
            Assert.NotEmpty(host.FindAll(".lc-dialog input[type=file]"));
            Assert.Contains(Services.GetRequiredService<Radzen.NotificationService>().Messages, m => m.Summary!.Contains("could not be found"));
        }, TimeSpan.FromSeconds(10));
    }

    private static AngleSharp.Dom.IElement UploadButton(IRenderedComponent<ComponentHost> host) =>
        host.FindAll(".lc-dialog button").First(b => b.TextContent.Trim() == "Upload");
}
