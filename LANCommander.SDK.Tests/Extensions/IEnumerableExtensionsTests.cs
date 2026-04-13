using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class IEnumerableExtensionsTests
{
    // ── OrderByTitle ───────────────────────────────────────────────────────────

    [Fact]
    public void OrderByTitle_StripsLeadingThe_ForSortKey()
    {
        var titles = new[] { "Zelda", "The Witcher", "Baldur's Gate" };

        var result = titles.OrderByTitle(t => t).Select(t => t).ToList();

        // "The Witcher" sorts as "Witcher", coming after "Zelda" is wrong —
        // actual order: Baldur's Gate, The Witcher, Zelda
        Assert.Equal("Baldur's Gate", result[0]);
        Assert.Equal("The Witcher",   result[1]);
        Assert.Equal("Zelda",         result[2]);
    }

    [Fact]
    public void OrderByTitle_StripsLeadingA_ForSortKey()
    {
        var titles = new[] { "Call of Duty", "A Plague Tale", "Doom" };

        var result = titles.OrderByTitle(t => t).ToList();

        // "A Plague Tale" sorts as "Plague Tale"
        Assert.Equal("Call of Duty", result[0]);
        Assert.Equal("Doom",         result[1]);
        Assert.Equal("A Plague Tale", result[2]);
    }

    [Fact]
    public void OrderByTitle_StripsLeadingAn_ForSortKey()
    {
        var titles = new[] { "Batman", "An Elder Tale", "Civilization" };

        var result = titles.OrderByTitle(t => t).ToList();

        // "An Elder Tale" sorts as "Elder Tale"
        Assert.Equal("Batman",        result[0]);
        Assert.Equal("Civilization",  result[1]);
        Assert.Equal("An Elder Tale", result[2]);
    }

    [Fact]
    public void OrderByTitle_ArticleStrippingIsCaseInsensitive()
    {
        var titles = new[] { "Zelda", "the Witcher", "Baldur's Gate" };

        var result = titles.OrderByTitle(t => t).ToList();

        Assert.Equal("Baldur's Gate", result[0]);
        Assert.Equal("the Witcher",   result[1]);
        Assert.Equal("Zelda",         result[2]);
    }

    [Fact]
    public void OrderByTitle_WordStartingWithArticleButNoTrailingSpace_IsNotStripped()
    {
        // "Another World" starts with "An" but "Another" ≠ "an "
        // "There" starts with "The" but "There" ≠ "the "
        var titles = new[] { "Another World", "There Will Be Blood", "Abzu" };

        var result = titles.OrderByTitle(t => t).ToList();

        Assert.Equal("Abzu",                result[0]);
        Assert.Equal("Another World",       result[1]);
        Assert.Equal("There Will Be Blood", result[2]);
    }

    [Fact]
    public void OrderByTitle_Descending_ReversesOrder()
    {
        var titles = new[] { "The Witcher", "Baldur's Gate", "Zelda" };

        var result = titles.OrderByTitle(t => t, SortDirection.Descending).ToList();

        Assert.Equal("Zelda",         result[0]);
        Assert.Equal("The Witcher",   result[1]);
        Assert.Equal("Baldur's Gate", result[2]);
    }

    [Fact]
    public void OrderByTitle_WorksWithObjectKeySelector()
    {
        var games = new[]
        {
            new { Id = 1, Title = "The Last of Us" },
            new { Id = 2, Title = "Among Us" },
            new { Id = 3, Title = "Hades" },
        };

        var result = games.OrderByTitle(g => g.Title).ToList();

        // "The Last of Us" → "Last of Us", "Among Us" → "Among Us", "Hades" → "Hades"
        Assert.Equal(2, result[0].Id); // Among Us
        Assert.Equal(3, result[1].Id); // Hades
        Assert.Equal(1, result[2].Id); // The Last of Us
    }

    // ── OrderBy (SortDirection overload) ──────────────────────────────────────

    [Fact]
    public void OrderBy_Ascending_SortsSmallestFirst()
    {
        var numbers = new[] { 3, 1, 4, 1, 5, 9, 2 };

        var result = numbers.OrderBy(n => n, SortDirection.Ascending).ToList();

        Assert.Equal(new[] { 1, 1, 2, 3, 4, 5, 9 }, result);
    }

    [Fact]
    public void OrderBy_Descending_SortsLargestFirst()
    {
        var numbers = new[] { 3, 1, 4, 1, 5, 9, 2 };

        var result = numbers.OrderBy(n => n, SortDirection.Descending).ToList();

        Assert.Equal(new[] { 9, 5, 4, 3, 2, 1, 1 }, result);
    }

    [Fact]
    public void OrderBy_OnStrings_Ascending()
    {
        var words = new[] { "banana", "apple", "cherry" };

        var result = words.OrderBy(w => w, SortDirection.Ascending).ToList();

        Assert.Equal(new[] { "apple", "banana", "cherry" }, result);
    }

    // ── HasAny (no predicate) ──────────────────────────────────────────────────

    [Fact]
    public void HasAny_WithNonEmptyCollection_ReturnsTrue()
    {
        var list = new[] { 1, 2, 3 };

        Assert.True(list.HasAny());
    }

    [Fact]
    public void HasAny_WithEmptyCollection_ReturnsFalse()
    {
        var list = Array.Empty<int>();

        Assert.False(list.HasAny());
    }

    [Fact]
    public void HasAny_WithNullCollection_ReturnsFalse()
    {
        IEnumerable<int>? list = null;

        Assert.False(list.HasAny());
    }

    // ── HasAny (with predicate) ────────────────────────────────────────────────

    [Fact]
    public void HasAny_WithPredicate_WhenMatchExists_ReturnsTrue()
    {
        var list = new[] { 1, 2, 3, 4, 5 };

        Assert.True(list.HasAny(n => n > 4));
    }

    [Fact]
    public void HasAny_WithPredicate_WhenNoMatch_ReturnsFalse()
    {
        var list = new[] { 1, 2, 3 };

        Assert.False(list.HasAny(n => n > 10));
    }

    [Fact]
    public void HasAny_WithPredicate_OnNullCollection_ReturnsFalse()
    {
        IEnumerable<int>? list = null;

        Assert.False(list.HasAny(n => n > 0));
    }

    [Fact]
    public void HasAny_WithPredicate_OnEmptyCollection_ReturnsFalse()
    {
        var list = Array.Empty<int>();

        Assert.False(list.HasAny(n => n > 0));
    }
}
