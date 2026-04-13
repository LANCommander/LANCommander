using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class ListExtensionsTests
{
    // ── RemoveRange ────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveRange_RemovesAllSpecifiedItems()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };

        list.RemoveRange(new[] { 2, 4 });

        Assert.Equal(new[] { 1, 3, 5 }, list);
    }

    [Fact]
    public void RemoveRange_ItemsNotInCollection_AreIgnored()
    {
        var list = new List<int> { 1, 2, 3 };

        list.RemoveRange(new[] { 99, 100 });

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void RemoveRange_WithNullItemsToRemove_DoesNotThrow()
    {
        var list = new List<int> { 1, 2, 3 };

        list.RemoveRange(null);

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void RemoveRange_WithEmptyItemsToRemove_LeavesCollectionUnchanged()
    {
        var list = new List<int> { 1, 2, 3 };

        list.RemoveRange(Array.Empty<int>());

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void RemoveRange_RemovesAllItems_WhenAllSpecified()
    {
        var list = new List<string> { "a", "b", "c" };

        list.RemoveRange(new[] { "a", "b", "c" });

        Assert.Empty(list);
    }

    [Fact]
    public void RemoveRange_OnlyRemovesFirstOccurrence_ForDuplicates()
    {
        // List<T>.Remove removes only the first matching element.
        var list = new List<int> { 1, 2, 2, 3 };

        list.RemoveRange(new[] { 2 });

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void RemoveRange_WorksWithReferenceTypes()
    {
        var a = new object();
        var b = new object();
        var c = new object();
        var list = new List<object> { a, b, c };

        list.RemoveRange(new[] { b });

        Assert.Equal(new[] { a, c }, list);
    }

    // ── RemoveAll ──────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveAll_RemovesItemsMatchingPredicate()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };

        list.RemoveAll(n => n % 2 == 0);

        Assert.Equal(new[] { 1, 3, 5 }, list);
    }

    [Fact]
    public void RemoveAll_KeepsItemsNotMatchingPredicate()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };

        list.RemoveAll(n => n > 10);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list);
    }

    [Fact]
    public void RemoveAll_WithNullPredicate_ThrowsArgumentNullException()
    {
        var list = new List<int> { 1, 2, 3 };

        Assert.Throws<ArgumentNullException>(() => list.RemoveAll(null!));
    }

    [Fact]
    public void RemoveAll_WithEmptyCollection_DoesNotThrow()
    {
        var list = new List<int>();

        var ex = Record.Exception(() => list.RemoveAll(n => n > 0));

        Assert.Null(ex);
        Assert.Empty(list);
    }

    [Fact]
    public void RemoveAll_WithAllMatchingPredicate_EmptiesCollection()
    {
        var list = new List<int> { 1, 2, 3 };

        list.RemoveAll(_ => true);

        Assert.Empty(list);
    }

    [Fact]
    public void RemoveAll_RemovesItemsCorrectlyWhenIteratingBackwards()
    {
        // Verify that removing while iterating backwards doesn't skip or double-remove.
        var list = new List<int> { 1, 2, 3, 4, 5, 6 };

        list.RemoveAll(n => n % 3 == 0);

        Assert.Equal(new[] { 1, 2, 4, 5 }, list);
    }

    [Fact]
    public void RemoveAll_WorksWithStrings()
    {
        var list = new List<string> { "alpha", "beta", "gamma", "delta" };

        list.RemoveAll(s => s.StartsWith("b") || s.StartsWith("d"));

        Assert.Equal(new[] { "alpha", "gamma" }, list);
    }
}
