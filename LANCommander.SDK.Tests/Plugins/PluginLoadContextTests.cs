using LANCommander.SDK.Plugins;

namespace LANCommander.SDK.Tests.Plugins;

public class PluginLoadContextTests
{
    [Theory]
    [InlineData("LANCommander.SDK")]
    [InlineData("LANCommander.Server.Plugins")]
    [InlineData("LANCommander.Server.ImportExport")]
    [InlineData("LANCommander.Server.ImportExport.SomeFutureAssembly")]
    public void HostContractAssembliesAreShared(string assemblyName) =>
        Assert.True(PluginLoadContext.IsShared(assemblyName));

    [Fact]
    public void PluginPrivateAssembliesAreNotShared() =>
        Assert.False(PluginLoadContext.IsShared("Example.Plugin.Dependency"));
}
