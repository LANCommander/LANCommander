using LANCommander.Launcher.Helpers;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Only server thumbnails get a size; everything else the image loaders see must pass through untouched.
/// </summary>
public class ThumbnailUrlTests
{
    private const string Thumbnail = "http://server:1337/api/Media/0b9f7d6e-1c7a-4a5e-9d0e-3f1b2c4d5e6f/Thumbnail";

    [Fact]
    public void AddsTheWidthToAThumbnailUrl() =>
        Assert.Equal($"{Thumbnail}?width=368", ThumbnailUrl.WithSize(Thumbnail, 368, 0));

    [Fact]
    public void FallsBackToTheHeightWhenNoWidthIsGiven() =>
        Assert.Equal($"{Thumbnail}?height=300", ThumbnailUrl.WithSize(Thumbnail, 0, 300));

    [Fact]
    public void LeavesAnUnsizedRequestAlone() =>
        Assert.Equal(Thumbnail, ThumbnailUrl.WithSize(Thumbnail, 0, 0));

    [Theory]
    [InlineData("http://server:1337/api/Media/0b9f7d6e-1c7a-4a5e-9d0e-3f1b2c4d5e6f/Stream")]
    [InlineData("http://server:1337/api/Media/0b9f7d6e-1c7a-4a5e-9d0e-3f1b2c4d5e6f/Download?fileId=1")]
    [InlineData(Thumbnail + "?width=128")]
    [InlineData(@"C:\Users\me\AppData\Local\LANCommander\Media\cover.jpg")]
    public void LeavesOtherSourcesAlone(string source) =>
        Assert.Equal(source, ThumbnailUrl.WithSize(source, 368, 0));
}
