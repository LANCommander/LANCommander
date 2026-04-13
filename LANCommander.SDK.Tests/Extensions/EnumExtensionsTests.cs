using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class EnumExtensionsTests
{
    private enum Color { Red, Green, Blue }

    // ── ValueIsIn ──────────────────────────────────────────────────────────────

    [Fact]
    public void ValueIsIn_WhenValueIsInList_ReturnsTrue()
    {
        var result = Color.Green.ValueIsIn(Color.Red, Color.Green, Color.Blue);

        Assert.True(result);
    }

    [Fact]
    public void ValueIsIn_WhenValueIsNotInList_ReturnsFalse()
    {
        var result = Color.Blue.ValueIsIn(Color.Red, Color.Green);

        Assert.False(result);
    }

    [Fact]
    public void ValueIsIn_WithEmptyList_ReturnsFalse()
    {
        var result = Color.Red.ValueIsIn();

        Assert.False(result);
    }

    [Fact]
    public void ValueIsIn_WithSingleMatchingValue_ReturnsTrue()
    {
        var result = Color.Red.ValueIsIn(Color.Red);

        Assert.True(result);
    }

    [Fact]
    public void ValueIsIn_WithSortDirectionEnum_WorksCorrectly()
    {
        var result = SortDirection.Descending.ValueIsIn(SortDirection.Ascending, SortDirection.Descending);

        Assert.True(result);
    }

    [Fact]
    public void ValueIsIn_WorksWithNonEnumTypes()
    {
        // The extension is generic — not restricted to enums.
        var result = "hello".ValueIsIn("world", "hello", "foo");

        Assert.True(result);
    }

    [Fact]
    public void ValueIsIn_WorksWithIntegers()
    {
        var result = 42.ValueIsIn(1, 2, 42, 100);

        Assert.True(result);
    }
}
