using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Models;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Places plugins add to import from: listed in the import dialog's switcher beside Upload and Local
/// File for the kinds of record they import, and finishing by handing over a package or by importing
/// themselves.
/// </summary>
[Collection("BUnit")]
public class PluginImportSourcesTests : BUnitTestContext
{
    private sealed class FakeSource(string id, Type component, params ManifestType[] types) : IImportSourceExtension
    {
        public string Id => id;
        public string Name => "Acme Library";
        public IReadOnlyCollection<ManifestType> SupportedTypes => types;
        public Type Component => component;
    }

    /// <summary>The package <see cref="LibraryPanel"/> hands over; set by each test.</summary>
    private static string? _packagePath;

    public sealed class LibraryPanel : ComponentBase
    {
        [Parameter] public ImportSourceContext? Context { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "acme-library");
            builder.AddContent(2, $"Importing {Context?.ManifestType}");
            builder.CloseElement();

            builder.OpenElement(3, "button");
            builder.AddAttribute(4, "class", "acme-use-package");
            builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => Context!.ImportPackageAsync(_packagePath!, deleteWhenDone: true)));
            builder.AddContent(6, "Use package");
            builder.CloseElement();

            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "class", "acme-finish");
            builder.AddAttribute(9, "onclick", EventCallback.Factory.Create(this, () => Context!.CompleteAsync(["Acme skipped a DLC"])));
            builder.AddContent(10, "Finish");
            builder.CloseElement();
        }
    }

    /// <summary>A component with no Context parameter, which the dialog can't hand anything to.</summary>
    public sealed class NoContextPanel : ComponentBase
    {
    }

    public PluginImportSourcesTests(BUnitServerFixture fixture) : base(fixture)
    {
        Services.AddSingleton(new PluginImportSources(
            [new FakeSource("com.acme.library", typeof(LibraryPanel), ManifestType.Game)],
            NullLogger<PluginImportSources>.Instance));
    }

    private IRenderedComponent<ComponentHost> OpenDialog(ManifestType type)
    {
        var host = Render<ComponentHost>();

        _ = Services.GetRequiredService<DialogService>().OpenAsync<ImportUploadDialog, ImportDialogOptions, bool>("Import", new ImportDialogOptions
        {
            ManifestType = type,
        }, new DialogSettings { NoFooter = true });

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog .lc-segmented-option")));

        return host;
    }

    private static List<string> Options(IRenderedComponent<ComponentHost> host) =>
        host.FindAll(".lc-dialog .lc-segmented-option").Select(o => o.TextContent.Trim()).ToList();

    private static void Choose(IRenderedComponent<ComponentHost> host, string option) =>
        host.FindAll(".lc-dialog .lc-segmented-option").First(o => o.TextContent.Trim() == option).Click();

    [Fact]
    public void UploadIsChosenFirstFollowedByPluginSources()
    {
        var host = OpenDialog(ManifestType.Game);

        Assert.Equal(["Upload", "Local File", "Acme Library"], Options(host));
        Assert.Equal("true", host.Find(".lc-dialog .lc-segmented-option").GetAttribute("aria-checked"));
        Assert.NotEmpty(host.FindAll(".lc-dialog input[type=file]"));
    }

    [Fact]
    public void SourcesAreOnlyOfferedForTheKindsTheyImport()
    {
        var host = OpenDialog(ManifestType.Tool);

        Assert.Equal(["Upload", "Local File"], Options(host));
    }

    [Fact]
    public void LocalFileShowsAPathOnTheServer()
    {
        var host = OpenDialog(ManifestType.Game);

        Choose(host, "Local File");

        host.WaitForAssertion(() =>
        {
            Assert.Empty(host.FindAll(".lc-dialog input[type=file]"));
            Assert.NotEmpty(host.FindAll(".lc-dialog .file-picker input"));
        });
    }

    [Fact]
    public async Task PackageHandedOverIsImportedAndDeleted()
    {
        var package = Path.Combine(ImportPackages.TempDirectory(), $"{Guid.NewGuid():N}.lcx");
        File.Copy(await ImportPackages.WriteAsync(ImportPackages.Game()), package);
        _packagePath = package;

        var host = OpenDialog(ManifestType.Game);

        Choose(host, "Acme Library");

        host.WaitForAssertion(() => Assert.Contains("Importing Game", host.Find(".acme-library").TextContent));

        host.Find(".acme-use-package").Click();

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog .lc-tree-checkbox")), TimeSpan.FromSeconds(10));

        host.FindAll(".lc-dialog button").First(b => b.TextContent.Trim() == "Import").Click();

        host.WaitForAssertion(() => Assert.Contains("Import Complete!", host.Markup), TimeSpan.FromSeconds(15));
        Assert.False(File.Exists(package));
    }

    [Fact]
    public void SourceThatImportsItselfShowsTheResult()
    {
        var host = OpenDialog(ManifestType.Game);

        Choose(host, "Acme Library");
        host.WaitForAssertion(() => host.Find(".acme-finish").Click());

        host.WaitForAssertion(() =>
        {
            Assert.Contains("Import Complete!", host.Markup);
            Assert.Contains("Acme skipped a DLC", host.Markup);
        });
    }

    [Fact]
    public void InvalidSourcesAreLeftOut()
    {
        var sources = new PluginImportSources(
        [
            new FakeSource("com.acme.library", typeof(LibraryPanel), ManifestType.Game),
            new FakeSource("com.acme.library", typeof(LibraryPanel), ManifestType.Game),
            new FakeSource("has spaces", typeof(LibraryPanel), ManifestType.Game),
            new FakeSource("com.acme.nocontext", typeof(NoContextPanel), ManifestType.Game),
            new FakeSource("com.acme.notacomponent", typeof(string), ManifestType.Game),
            new FakeSource("com.acme.nothing", typeof(LibraryPanel)),
        ], NullLogger<PluginImportSources>.Instance);

        Assert.Equal(["com.acme.library"], sources.For(null).Select(s => s.Id));
        Assert.Empty(sources.For(ManifestType.Redistributable));
    }
}
