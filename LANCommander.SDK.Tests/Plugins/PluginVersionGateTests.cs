using LANCommander.SDK.Plugins;

namespace LANCommander.SDK.Tests.Plugins;

public class PluginVersionGateTests
{
    [Theory]
    [InlineData("1.1.0", null, null, true)]           // no bounds → compatible
    [InlineData("1.1.0", "1.0.0", null, true)]         // above min
    [InlineData("1.1.0", "1.1.0", null, true)]         // equal to min (inclusive)
    [InlineData("1.0.0", "1.1.0", null, false)]        // below min
    [InlineData("1.5.0", null, "2.0.0", true)]         // below max
    [InlineData("2.0.0", null, "2.0.0", true)]         // equal to max (inclusive)
    [InlineData("2.1.0", null, "2.0.0", false)]        // above max
    [InlineData("1.5.0", "1.0.0", "2.0.0", true)]      // within range
    [InlineData("0.9.0", "1.0.0", "2.0.0", false)]     // below range
    [InlineData("2.5.0", "1.0.0", "2.0.0", false)]     // above range
    public void IsVersionCompatible_EvaluatesBounds(string hostVersion, string? min, string? max, bool expected)
    {
        Assert.Equal(expected, PluginLoaderService.IsVersionCompatible(hostVersion, min, max));
    }

    [Fact]
    public void IsVersionCompatible_UnparseableHostVersion_DoesNotBlock()
    {
        Assert.True(PluginLoaderService.IsVersionCompatible("not-a-version", "1.0.0", "2.0.0"));
    }
}
