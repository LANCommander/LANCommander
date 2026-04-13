using LANCommander.SDK.Extensions;
using SdkUriExtensions = LANCommander.SDK.Extensions.UriExtensions;

namespace LANCommander.SDK.Tests.Extensions;

public class UriExtensionsTests
{
    // ── Join ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Join_AppendsSingleSegment()
    {
        var base_ = new Uri("http://example.com");

        var result = base_.Join("api");

        Assert.Equal("http://example.com/api", result.ToString());
    }

    [Fact]
    public void Join_AppendsMultipleSegments()
    {
        var base_ = new Uri("http://example.com");

        var result = base_.Join("api", "v1", "games");

        Assert.Equal("http://example.com/api/v1/games", result.ToString());
    }

    [Fact]
    public void Join_StripsTrailingSlashFromBase()
    {
        var base_ = new Uri("http://example.com/");

        var result = base_.Join("api");

        Assert.Equal("http://example.com/api", result.ToString());
    }

    [Fact]
    public void Join_StripsLeadingSlashFromSegment()
    {
        var base_ = new Uri("http://example.com");

        var result = base_.Join("/api");

        Assert.Equal("http://example.com/api", result.ToString());
    }

    [Fact]
    public void Join_StripsLeadingAndTrailingSlashesFromSegments()
    {
        var base_ = new Uri("http://example.com/");

        var result = base_.Join("/api/", "/v1/");

        Assert.Equal("http://example.com/api/v1", result.ToString());
    }

    [Fact]
    public void Join_WithBaseContainingPath_AppendsCorrectly()
    {
        var base_ = new Uri("http://example.com/root");

        var result = base_.Join("sub");

        Assert.Equal("http://example.com/root/sub", result.ToString());
    }

    // ── CreateUri ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateUri_WithFullHttpUri_ReturnsSameUri()
    {
        var result = SdkUriExtensions.CreateUri("http://example.com");

        Assert.Equal("http://example.com/", result.ToString());
    }

    [Fact]
    public void CreateUri_WithFullHttpsUri_ReturnsSameUri()
    {
        var result = SdkUriExtensions.CreateUri("https://example.com");

        Assert.Equal("https://example.com/", result.ToString());
    }

    [Fact]
    public void CreateUri_WithNoScheme_PrependsHttp()
    {
        var result = SdkUriExtensions.CreateUri("example.com");

        Assert.Equal(Uri.UriSchemeHttp, result.Scheme);
        Assert.Equal("example.com", result.Host);
    }

    [Fact]
    public void CreateUri_WithNullInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SdkUriExtensions.CreateUri(null));
    }

    [Fact]
    public void CreateUri_WithEmptyInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SdkUriExtensions.CreateUri(""));
    }

    [Fact]
    public void CreateUri_WithWhitespaceInput_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SdkUriExtensions.CreateUri("   "));
    }

    [Fact]
    public void CreateUri_WithUriContainingPort_PreservesPort()
    {
        var result = SdkUriExtensions.CreateUri("http://localhost:1337");

        Assert.Equal(1337, result.Port);
    }

    [Fact]
    public void CreateUri_WithIpAddress_Works()
    {
        var result = SdkUriExtensions.CreateUri("192.168.1.1");

        Assert.Equal(Uri.UriSchemeHttp, result.Scheme);
        Assert.Equal("192.168.1.1", result.Host);
    }

    // ── TryCreateUri ──────────────────────────────────────────────────────────

    [Fact]
    public void TryCreateUri_WithValidUri_ReturnsTrueAndSetsResult()
    {
        var success = SdkUriExtensions.TryCreateUri("http://example.com", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("http://example.com/", result.ToString());
    }

    [Fact]
    public void TryCreateUri_WithNoScheme_PrependsHttpAndReturnsTrue()
    {
        var success = SdkUriExtensions.TryCreateUri("example.com", out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(Uri.UriSchemeHttp, result.Scheme);
    }

    [Fact]
    public void TryCreateUri_WithNullInput_ReturnsFalse()
    {
        var success = SdkUriExtensions.TryCreateUri(null, out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryCreateUri_WithEmptyInput_ReturnsFalse()
    {
        var success = SdkUriExtensions.TryCreateUri("", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryCreateUri_WithWhitespace_ReturnsFalse()
    {
        var success = SdkUriExtensions.TryCreateUri("   ", out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryCreateUri_WithHttpsScheme_PreservesScheme()
    {
        var success = SdkUriExtensions.TryCreateUri("https://secure.example.com", out var result);

        Assert.True(success);
        Assert.Equal(Uri.UriSchemeHttps, result!.Scheme);
    }

    [Fact]
    public void TryCreateUri_WithPortNumber_PreservesPort()
    {
        var success = SdkUriExtensions.TryCreateUri("http://localhost:8080", out var result);

        Assert.True(success);
        Assert.Equal(8080, result!.Port);
    }
}
