using System.IO.Compression;
using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Covers a report that after uploading an archive the dialog stayed open at 100% and had to be
/// closed by hand.
/// </summary>
[Collection("BUnit")]
public class ArchiveUploadDialogComponentTests : BUnitTestContext
{
    public ArchiveUploadDialogComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    /// <summary>What the chunk upload endpoints leave behind: a stored file and its archive row.</summary>
    private async Task<string> StageUploadedArchiveAsync()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var storageLocation = (await scope.ServiceProvider.GetRequiredService<StorageLocationService>()
            .GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var archive = await scope.ServiceProvider.GetRequiredService<ArchiveService>().AddAsync(new Archive
        {
            ObjectKey = Guid.NewGuid().ToString(),
            Version = string.Empty,
            StorageLocationId = storageLocation.Id,
        });

        await using (var fs = File.Create(Path.Combine(storageLocation.Path, archive.ObjectKey)))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        await using (var writer = new StreamWriter(zip.CreateEntry("game.exe").Open()))
            await writer.WriteAsync("binary");

        return archive.ObjectKey;
    }

    [Fact]
    public async Task DialogClosesOnceTheUploadCompletes()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var redistributable = await scope.ServiceProvider.GetRequiredService<RedistributableService>()
            .AddAsync(new Redistributable { Name = $"Upload {Guid.NewGuid():N}" });

        Guid? uploaded = null;

        // The dialog logs, rather than throws, when it can't save the upload; show why in a failure
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ArchiveUploadDialog>>(new CapturingLogger<ArchiveUploadDialog>(errors));

        var host = Render<ComponentHost>();

        _ = Services.GetRequiredService<DialogService>().OpenAsync<ArchiveUploadDialog, ArchiveUploadOptions, bool>("Upload Archive", new ArchiveUploadOptions
        {
            RedistributableId = redistributable.Id,
            OnUploaded = id =>
            {
                uploaded = id;
                return Task.CompletedTask;
            },
        }, new DialogSettings { NoFooter = true });

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog input[type=file]")), TimeSpan.FromSeconds(10));

        // The dialog re-renders when its storage location finishes loading, which on a slower database
        // can land between finding the version box and typing into it; doing both on the renderer's
        // thread keeps the typing from going to a box that's already been replaced
        await host.InvokeAsync(() => host.FindAll(".lc-dialog input").First(i => i.GetAttribute("type") != "file").Input("1.0"));
        host.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("zip bytes", "game.zip"));

        // Upload is enabled once the version and file have reached the dialog. bUnit clicks disabled
        // buttons too, so clicking any sooner could upload with no version, which no user can do.
        host.WaitForAssertion(() => Assert.False(UploadButton(host).HasAttribute("disabled")), TimeSpan.FromSeconds(10));
        UploadButton(host).Click();

        var tracker = Services.GetRequiredService<UploadTracker>();
        host.WaitForAssertion(() => Assert.Single(tracker.ActiveUploads), TimeSpan.FromSeconds(5));

        var objectKey = await StageUploadedArchiveAsync();

        // The browser reports the finished upload
        await host.InvokeAsync(() => tracker.JSOnUploadComplete(tracker.ActiveUploads.Keys.Single(), objectKey));

        host.WaitForAssertion(() =>
        {
            Assert.True(uploaded != null, $"The upload was never saved. {String.Join(" | ", errors)}");
            Assert.Empty(host.FindAll(".lc-dialog"));
        }, TimeSpan.FromSeconds(10));
    }

    private static AngleSharp.Dom.IElement UploadButton(IRenderedComponent<ComponentHost> host)
        => host.FindAll(".lc-dialog button").First(b => b.TextContent.Trim() == "Upload");

    private sealed class CapturingLogger<T>(System.Collections.Concurrent.ConcurrentQueue<string> errors) : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Error)
                errors.Enqueue($"{formatter(state, exception)} {exception?.GetBaseException().Message}");
        }
    }
}
