using Bunit;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins.Installation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NuGet.Frameworks;
using PluginsSettings = LANCommander.Server.UI.Pages.Settings.Plugins.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>The plugins settings page, over a plugins folder of its own so the server's is never touched.</summary>
[Collection("BUnit")]
public class PluginsSettingsComponentTests : BUnitTestContext
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-plugins-page-{Guid.NewGuid():N}");
    private readonly PluginStaging _staging;

    public PluginsSettingsComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
        _staging = new PluginStaging(Path.Combine(_root, "Plugins"), Path.Combine(_root, "PluginStaging"));

        UseServerVersion("2.2.0");
    }

    private void UseServerVersion(string version) =>
        Services.AddSingleton(new PluginPackageService(
            Fixture.Factory.RealServices.GetRequiredService<SettingsProvider<Settings.Settings>>(),
            new PluginLoaderService(),
            _staging,
            NullLogger<PluginPackageService>.Instance,
            version,
            NuGetFramework.Parse("net10.0")));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void ShowsTheTabsAndTheUploadArea()
    {
        var cut = Render<PluginsSettings>();

        Assert.Contains("Installed", cut.Markup);
        Assert.Contains("Browse", cut.Markup);
        Assert.Contains("Sources", cut.Markup);
        Assert.Contains("Click or drag .nupkg files here to install", cut.Markup);
        Assert.Contains("No plugins are installed", cut.Markup);
        Assert.Equal(".nupkg", cut.Find("input[type=file]").GetAttribute("accept"));
        Assert.DoesNotContain("Restart required", cut.Markup);
    }

    [Fact]
    public void BadgesOfficialPlugins()
    {
        var official = _staging.GetStagedPath("LANCommander.Plugins.Steam");
        Directory.CreateDirectory(official);
        new PluginPackageRecord { Id = "LANCommander.Plugins.Steam", Version = "2.2.0-alpha", Title = "Steam", IsOfficial = true }.Write(official);

        var other = _staging.GetStagedPath("Acme.Widgets");
        Directory.CreateDirectory(other);
        new PluginPackageRecord { Id = "Acme.Widgets", Version = "1.0.0", Title = "Widgets" }.Write(other);

        var cut = Render<PluginsSettings>();

        var rows = cut.FindAll("tr").Where(r => r.TextContent.Contains("Steam") || r.TextContent.Contains("Widgets")).ToList();
        Assert.Contains("Official", rows.Single(r => r.TextContent.Contains("Steam")).TextContent);
        Assert.DoesNotContain("Official", rows.Single(r => r.TextContent.Contains("Widgets")).TextContent);
    }

    [Fact]
    public void APrereleaseServerAlwaysOffersPrereleasePlugins()
    {
        UseServerVersion("2.2.0-alpha.2");

        var cut = Render<PluginsSettings>();

        // Only the selected tab is rendered
        cut.FindAll("[role=tab]").First(tab => tab.TextContent.Contains("Sources")).Click();

        Assert.Contains("Always on while the server is a prerelease itself.", cut.Markup);
    }

    [Fact]
    public void ShowsStagedAndManualPlugins()
    {
        var staged = _staging.GetStagedPath("Acme.Widgets");
        Directory.CreateDirectory(staged);
        new PluginPackageRecord { Id = "Acme.Widgets.Plugin", Version = "1.2.0", Title = "Acme Widgets", SourceName = "nuget.org" }.Write(staged);

        Directory.CreateDirectory(_staging.GetInstalledPath("HandCopied"));

        var cut = Render<PluginsSettings>();

        Assert.Contains("Restart required", cut.Markup);
        Assert.Contains("Acme Widgets", cut.Markup);
        Assert.Contains("Installs on restart", cut.Markup);
        Assert.Contains("HandCopied", cut.Markup);
        Assert.Contains("Manual", cut.Markup);
        Assert.Contains("Not loaded", cut.Markup);
    }
}
