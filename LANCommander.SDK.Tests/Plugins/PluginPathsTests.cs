using LANCommander.SDK.Plugins;

namespace LANCommander.SDK.Tests.Plugins;

public class PluginPathsTests
{
    [Fact]
    public void TheDataDirectoryIsInTheFolderTheAssemblyWasLoadedFrom()
    {
        var folder = Path.GetDirectoryName(typeof(PluginPathsTests).Assembly.Location)!;

        Assert.Equal(Path.Combine(folder, PluginManifest.DataFolderName), PluginPaths.GetDataDirectory<PluginPathsTests>());
    }
}
