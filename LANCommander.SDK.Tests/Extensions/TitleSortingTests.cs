using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class TitleSortingTests
{
    [Theory]
    [InlineData("The Witcher 3", "Witcher 3")]
    [InlineData("the witcher 3", "witcher 3")]
    [InlineData("A Plague Tale", "Plague Tale")]
    [InlineData("An Untitled Story", "Untitled Story")]
    [InlineData("  The Sims", "Sims")]
    [InlineData("Theme Hospital", "Theme Hospital")]
    [InlineData("Anno 1602", "Anno 1602")]
    [InlineData("Another World", "Another World")]
    [InlineData("The", "The")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ToSortableTitle_StripsLeadingArticle(string? title, string expected)
    {
        Assert.Equal(expected, title.ToSortableTitle());
    }

    [Fact]
    public void ToSortableTitle_PrefersExplicitSortTitle()
    {
        Assert.Equal("Witcher", LANCommander.SDK.Extensions.StringExtensions.ToSortableTitle("The Witcher", "Something Else"));
        Assert.Equal("Witcher", LANCommander.SDK.Extensions.StringExtensions.ToSortableTitle("  ", "The Witcher"));
    }

    [Fact]
    public void OrderByTitle_IgnoresArticlesAndCase()
    {
        string[] titles = ["The Witcher", "age of empires", "A Plague Tale", "Baldur's Gate", "The Elder Scrolls"];

        Assert.Equal(
            ["age of empires", "Baldur's Gate", "The Elder Scrolls", "A Plague Tale", "The Witcher"],
            titles.OrderByTitle(t => t).ToArray());

        Assert.Equal(
            ["The Witcher", "A Plague Tale", "The Elder Scrolls", "Baldur's Gate", "age of empires"],
            titles.OrderByTitle(t => t, SortDirection.Descending).ToArray());
    }
}
