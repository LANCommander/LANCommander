using LANCommander.Server.Plugins;
using LANCommander.Server.UI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.UI.Tests.Components;

public class PluginSettingsLinksTests
{
    [Fact]
    public void LinksEachPluginToItsSettingsPage()
    {
        var links = PluginSettingsLinks.Resolve(
        [
            new Link("com.acme.widgets", "/Plugins/Widgets/Settings"),
            new Link("com.acme.gadgets", "/Plugins/Gadgets/Settings"),
        ], OwnerOf, NullLogger.Instance);

        Assert.Equal("/Plugins/Widgets/Settings", links["com.acme.widgets"]);
        Assert.Equal("/Plugins/Gadgets/Settings", links["COM.ACME.GADGETS"]);
    }

    [Fact]
    public void DropsLinksOutsidePluginRoutes()
    {
        var links = PluginSettingsLinks.Resolve(
        [
            new Link("com.acme.widgets", "/Settings/Users"),
            new Link("com.acme.gadgets", ""),
        ], OwnerOf, NullLogger.Instance);

        Assert.Empty(links);
    }

    [Fact]
    public void DropsLinksNoPluginOwns()
    {
        var links = PluginSettingsLinks.Resolve([new Link(null, "/Plugins/Orphan")], OwnerOf, NullLogger.Instance);

        Assert.Empty(links);
    }

    [Fact]
    public void DropsEveryLinkOfAPluginThatRegistersMoreThanOne()
    {
        var links = PluginSettingsLinks.Resolve(
        [
            new Link("com.acme.widgets", "/Plugins/Widgets/Settings"),
            new Link("com.acme.widgets", "/Plugins/Widgets/Other"),
            new Link("com.acme.gadgets", "/Plugins/Gadgets/Settings"),
        ], OwnerOf, NullLogger.Instance);

        Assert.Equal(["com.acme.gadgets"], links.Keys);
    }

    static string? OwnerOf(IPluginSettingsExtension extension) => ((Link)extension).PluginId;

    private sealed class Link(string? pluginId, string href) : IPluginSettingsExtension
    {
        public string? PluginId => pluginId;
        public string Href => href;
    }
}
