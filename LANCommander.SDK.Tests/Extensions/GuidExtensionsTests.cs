using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class GuidExtensionsTests
{
    // ── IsNullOrEmpty ──────────────────────────────────────────────────────────

    [Fact]
    public void IsNullOrEmpty_WithEmptyGuid_ReturnsTrue()
    {
        var result = Guid.Empty.IsNullOrEmpty();

        Assert.True(result);
    }

    [Fact]
    public void IsNullOrEmpty_WithDefaultGuid_ReturnsTrue()
    {
        // default(Guid) == Guid.Empty
        var guid = default(Guid);

        Assert.True(guid.IsNullOrEmpty());
    }

    [Fact]
    public void IsNullOrEmpty_WithNewGuid_ReturnsFalse()
    {
        var result = Guid.NewGuid().IsNullOrEmpty();

        Assert.False(result);
    }

    [Fact]
    public void IsNullOrEmpty_WithKnownNonEmptyGuid_ReturnsFalse()
    {
        var guid = new Guid("12345678-1234-1234-1234-123456789012");

        Assert.False(guid.IsNullOrEmpty());
    }
}
