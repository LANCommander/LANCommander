using LANCommander.SDK.Plugins;

namespace LANCommander.SDK.Tests.Plugins;

public class PluginPackageTypesTests
{
    [Theory]
    [InlineData(PluginHost.Server, new[] { "LANCommander.Plugin", "LANCommander.Plugin.Server" })]
    [InlineData(PluginHost.Launcher, new[] { "LANCommander.Plugin", "LANCommander.Plugin.Launcher" })]
    [InlineData(PluginHost.Server | PluginHost.Launcher, new[] { "LANCommander.Plugin", "LANCommander.Plugin.Server", "LANCommander.Plugin.Launcher" })]
    public void All_DeclaresTheBaseTypeAndOnePerHost(PluginHost hosts, string[] expected)
    {
        Assert.Equal(expected, PluginPackageTypes.All(hosts));
    }

    [Fact]
    public void For_IsTheTypeEachHostSearchesFor()
    {
        Assert.Equal("LANCommander.Plugin.Server", PluginPackageTypes.For(PluginHost.Server));
        Assert.Equal("LANCommander.Plugin.Launcher", PluginPackageTypes.For(PluginHost.Launcher));
        Assert.Throws<ArgumentOutOfRangeException>(() => PluginPackageTypes.For(PluginHost.Server | PluginHost.Launcher));
    }
}
