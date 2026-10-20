using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class TagsInputTests : ControlsTestContext
{
    public sealed record Genre(Guid Id, string Name);

    private static readonly Genre[] Genres =
    [
        new(Guid.NewGuid(), "Racing"),
        new(Guid.NewGuid(), "Shooter"),
        new(Guid.NewGuid(), "Strategy"),
    ];

    private IRenderedComponent<TagsInput<Genre>> RenderInput(
        IEnumerable<Genre>? values = null,
        Action<IEnumerable<Genre>>? changed = null,
        Func<string, Task<Genre>>? create = null) =>
        Render<TagsInput<Genre>>(p =>
        {
            p.Add(x => x.Items, Genres).Add(x => x.Text, g => g.Name).Add(x => x.Values, values ?? []);

            if (changed != null)
                p.Add(x => x.ValuesChanged, changed);

            if (create != null)
                p.Add(x => x.OnCreate, create);
        });

    private static void Type(IRenderedComponent<TagsInput<Genre>> input, string text)
    {
        input.Find(".lc-tags-input-search input").Change(text);
        input.Find(".lc-tags-input").KeyDown("Enter");
    }

    [Fact]
    public void ShowsEachValueAsARemovableTag()
    {
        var input = RenderInput([Genres[0], Genres[2]]);

        Assert.Equal(["Racing", "Strategy"], input.FindAll(".lc-tags-input-tag-text").Select(t => t.TextContent));
        Assert.Equal(2, input.FindAll(".lc-tags-input-remove").Count);
    }

    [Fact]
    public void Enter_AddsTheMatchingItem_IgnoringCase()
    {
        IEnumerable<Genre>? values = null;

        var input = RenderInput(changed: v => values = v);

        Type(input, "shooter");

        Assert.Equal([Genres[1]], values);
    }

    [Fact]
    public void Enter_WithNewText_CreatesAnItem()
    {
        IEnumerable<Genre>? values = null;

        var input = RenderInput(changed: v => values = v, create: name => Task.FromResult(new Genre(Guid.NewGuid(), name)));

        Type(input, "Puzzle");

        Assert.Equal(["Puzzle"], values?.Select(g => g.Name));
    }

    [Fact]
    public void Enter_WithNewText_AndNoCreate_AddsNothing()
    {
        IEnumerable<Genre>? values = null;

        var input = RenderInput(changed: v => values = v);

        Type(input, "Puzzle");

        Assert.Null(values);
    }

    [Fact]
    public void RemovingATag_ReportsTheRest()
    {
        IEnumerable<Genre>? values = null;

        var input = RenderInput([Genres[0], Genres[2]], v => values = v);

        input.FindAll(".lc-tags-input-remove")[0].Click();

        Assert.Equal([Genres[2]], values);
    }

    [Fact]
    public void DatePicker_ShowsTheDateInTheDefaultFormat()
    {
        var picker = Render<DatePicker<DateTime?>>(p => p.Add(x => x.Value, new DateTime(2004, 11, 16)));

        Assert.Equal("11/16/2004", picker.Find("input").GetAttribute("value"));
    }
}
