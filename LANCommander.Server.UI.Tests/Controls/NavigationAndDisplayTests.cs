using Bunit;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Controls;

public class NavigationAndDisplayTests : ControlsTestContext
{
    private sealed class FixedTimeProvider(DateTime utcNow) : LocalTimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private static RenderFragment Items(params (string Href, string Text)[] items) => builder =>
    {
        var sequence = 0;

        foreach (var (href, text) in items)
        {
            builder.OpenComponent<MenuItem>(sequence++);
            builder.AddComponentParameter(sequence++, nameof(MenuItem.Href), href);
            builder.AddComponentParameter(sequence++, nameof(MenuItem.ChildContent), (RenderFragment)(b => b.AddContent(0, text)));
            builder.CloseComponent();
        }
    };

    private IRenderedComponent<Menu> RenderMenuWithSubMenu() =>
        Render<Menu>(p => p.AddChildContent<SubMenu>(s => s
            .Add(x => x.Title, "Metadata")
            .Add(x => x.ChildContent, Items(("/Metadata/Genres", "Genres"), ("/Metadata/Tags", "Tags")))));

    [Fact]
    public void MenuItem_IsActive_OnItsPageAndBeneathIt()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/Games/123");

        var menu = Render<Menu>(p => p.Add(x => x.ChildContent, Items(("/Games", "Games"), ("/Servers", "Servers"))));

        var active = menu.FindAll("a.lc-menu-item-active");

        Assert.Equal("Games", Assert.Single(active).TextContent.Trim());
    }

    [Fact]
    public void SubMenu_OpensWhenAChildIsActive()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/Metadata/Tags");

        var menu = RenderMenuWithSubMenu();

        menu.WaitForAssertion(() => Assert.Contains("lc-submenu-open", menu.Find(".lc-submenu").ClassList));
        Assert.False(menu.Find(".lc-submenu-list").HasAttribute("hidden"));
    }

    [Fact]
    public void SubMenu_TogglesOnClick()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/Games");

        var menu = RenderMenuWithSubMenu();

        Assert.True(menu.Find(".lc-submenu-list").HasAttribute("hidden"));

        menu.Find(".lc-submenu-title").Click();

        Assert.False(menu.Find(".lc-submenu-list").HasAttribute("hidden"));
        Assert.Equal("true", menu.Find(".lc-submenu-title").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void MenuItem_WithoutHref_IsAButtonThatClicks()
    {
        var clicked = false;

        var menu = Render<Menu>(p => p.AddChildContent<MenuItem>(i => i
            .Add(x => x.OnClick, () => clicked = true)
            .Add(x => x.Danger, true)
            .AddChildContent("Delete")));

        menu.Find("button.lc-menu-item-content").Click();

        Assert.True(clicked);
        Assert.Contains("lc-menu-item-danger", menu.Find(".lc-menu-item").ClassList);
    }

    [Theory]
    [InlineData(MenuVariant.Default, null)]
    [InlineData(MenuVariant.Sidebar, "lc-menu-sidebar")]
    [InlineData(MenuVariant.Section, "lc-menu-section")]
    public void Menu_Variant_AddsItsClass(MenuVariant variant, string? expected)
    {
        var nav = Render<Menu>(p => p.Add(x => x.Variant, variant)).Find("nav");

        Assert.Contains("lc-menu-vertical", nav.ClassList);
        Assert.Equal(expected == "lc-menu-sidebar", nav.ClassList.Contains("lc-menu-sidebar"));
        Assert.Equal(expected == "lc-menu-section", nav.ClassList.Contains("lc-menu-section"));
    }

    [Fact]
    public void Menu_Horizontal_IgnoresVariant()
    {
        var nav = Render<Menu>(p => p.Add(x => x.Horizontal, true).Add(x => x.Variant, MenuVariant.Sidebar)).Find("nav");

        Assert.DoesNotContain("lc-menu-sidebar", nav.ClassList);
    }

    [Theory]
    [InlineData(1234, "1,234")]
    [InlineData(0, "0")]
    public void MenuItem_Count_ShowsTheFigure(int count, string expected)
    {
        var menu = Render<Menu>(p => p.AddChildContent<MenuItem>(i => i
            .Add(x => x.Href, "/Games")
            .Add(x => x.Count, count)
            .AddChildContent("Games")));

        Assert.Equal(expected, menu.Find(".lc-menu-item-count").TextContent);
        Assert.Equal("Games", menu.Find(".lc-menu-item-text").TextContent);
    }

    [Fact]
    public void MenuItem_WithoutCount_ShowsNoFigure()
    {
        var menu = Render<Menu>(p => p.AddChildContent<MenuItem>(i => i.Add(x => x.Href, "/Games").AddChildContent("Games")));

        Assert.Empty(menu.FindAll(".lc-menu-item-count"));
    }

    [Fact]
    public void MenuGroup_TitlesAndLabelsItsItems()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("/Games/1/Archives");

        var menu = Render<Menu>(p => p
            .Add(x => x.Variant, MenuVariant.Section)
            .AddChildContent<MenuGroup>(g => g
                .Add(x => x.Title, "Content")
                .Add(x => x.ChildContent, Items(("/Games/1/Archives", "Archives"), ("/Games/1/Keys", "Keys")))));

        var title = menu.Find(".lc-menu-group-title");
        var list = menu.Find(".lc-menu-group-list");

        Assert.Equal("Content", title.TextContent);
        Assert.Equal("group", list.GetAttribute("role"));
        Assert.Equal(title.Id, list.GetAttribute("aria-labelledby"));
        Assert.Equal(2, list.QuerySelectorAll(".lc-menu-item").Length);
        Assert.Equal("Archives", Assert.Single(menu.FindAll("a.lc-menu-item-active")).TextContent.Trim());
    }

    [Fact]
    public void MenuGroup_WithoutTitle_HasNoKicker()
    {
        var menu = Render<Menu>(p => p.AddChildContent<MenuGroup>(g => g.Add(x => x.ChildContent, Items(("/Games", "Games")))));

        Assert.Empty(menu.FindAll(".lc-menu-group-title"));
        Assert.False(menu.Find(".lc-menu-group-list").HasAttribute("aria-labelledby"));
    }

    [Theory]
    [InlineData(2, "Just now")]
    [InlineData(90, "A minute ago")]
    [InlineData(60 * 45, "45 minutes ago")]
    [InlineData(60 * 60 * 30, "Yesterday")]
    [InlineData(60 * 60 * 24 * 3, "3 days ago")]
    [InlineData(60 * 60 * 24 * 400, "A year ago")]
    public void LocalTime_DescribesElapsedTime(int seconds, string expected)
    {
        Assert.Equal(expected, LocalTime.Describe(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void LocalTime_Relative_UsesTimeProvider()
    {
        var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        Services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));

        var time = Render<LocalTime>(p => p.Add(x => x.Value, now.AddDays(-3)).Add(x => x.Relative, true));

        Assert.Equal("3 days ago", time.Find("time").TextContent);
    }

    [Fact]
    public void LocalTime_Absolute_ConvertsToBrowserZone()
    {
        var provider = new FixedTimeProvider(DateTime.UtcNow);
        provider.SetLocalTimeZone("America/New_York");
        Services.AddSingleton<TimeProvider>(provider);

        var time = Render<LocalTime>(p => p.Add(x => x.Value, new DateTime(2025, 1, 15, 17, 30, 0, DateTimeKind.Utc)));

        Assert.Equal("01/15/2025 12:30 PM", time.Find("time").TextContent);
    }

    [Fact]
    public void LocalTime_EmptyValue_RendersNothing()
    {
        Assert.Empty(Render<LocalTime>(p => p.Add(x => x.Value, DateTime.MinValue)).Markup.Trim());
        Assert.Empty(Render<LocalTime>().Markup.Trim());
    }

    [Fact]
    public void Panel_TogglesBody()
    {
        var panel = Render<Panel>(p => p.Add(x => x.Header, "Advanced").AddChildContent("<p class='body'>hidden</p>"));

        Assert.Empty(panel.FindAll("p.body"));

        panel.Find(".lc-panel-toggle").Click();

        Assert.NotNull(panel.Find("p.body"));
        Assert.Contains("lc-panel-open", panel.Find(".lc-panel").ClassList);
    }

    [Fact]
    public void Steps_MarkDoneCurrentAndPending()
    {
        var steps = Render<Steps>(p => p
            .Add(x => x.Current, 1)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                foreach (var (title, i) in new[] { "Database", "Administrator", "Storage" }.Select((t, i) => (t, i)))
                {
                    builder.OpenComponent<Step>(i * 2);
                    builder.AddComponentParameter(i * 2 + 1, nameof(Step.Title), title);
                    builder.CloseComponent();
                }
            })));

        var classes = steps.FindAll(".lc-step").Select(s => s.ClassList.First(c => c != "lc-step")).ToList();

        Assert.Equal(["lc-step-done", "lc-step-current", "lc-step-pending"], classes);
        Assert.Equal("step", steps.FindAll(".lc-step")[1].GetAttribute("aria-current"));
    }

    [Fact]
    public void Breadcrumb_LinksAllButTheLast()
    {
        var breadcrumb = Render<Breadcrumb>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<BreadcrumbItem>(0);
            builder.AddComponentParameter(1, nameof(BreadcrumbItem.Href), "/Games");
            builder.AddComponentParameter(2, nameof(BreadcrumbItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Games")));
            builder.CloseComponent();
            builder.OpenComponent<BreadcrumbItem>(3);
            builder.AddComponentParameter(4, nameof(BreadcrumbItem.ChildContent), (RenderFragment)(b => b.AddContent(0, "Arena Blitz")));
            builder.CloseComponent();
        })));

        Assert.Equal("/Games", breadcrumb.Find("a").GetAttribute("href"));
        Assert.Equal("page", breadcrumb.Find("span[aria-current]").GetAttribute("aria-current"));
    }

    [Theory]
    [InlineData(false, false, "rz-progressbar-primary")]
    [InlineData(true, false, "rz-progressbar-success")]
    [InlineData(false, true, "rz-progressbar-danger")]
    public void Progress_StatusMapsToStyle(bool success, bool danger, string expected)
    {
        var progress = Render<Progress>(p => p.Add(x => x.Value, 40).Add(x => x.Success, success).Add(x => x.Danger, danger));

        Assert.Contains(expected, progress.Find(".lc-progress").ClassList);
    }

    [Fact]
    public void Image_WithoutSource_ShowsFallback()
    {
        var image = Render<Image>(p => p.AddChildContent("<span class='fallback'>?</span>"));

        Assert.NotNull(image.Find(".fallback"));
        Assert.Empty(image.FindAll("img"));
    }

    [Fact]
    public void Image_FailingToLoad_ShowsFallback()
    {
        var image = Render<Image>(p => p.Add(x => x.Src, "/missing.png").AddChildContent("<span class='fallback'>?</span>"));

        image.Find("img").TriggerEvent("onerror", new EventArgs());

        Assert.NotNull(image.Find(".fallback"));
    }

    [Fact]
    public void Statistic_ShowsTitleAndValue()
    {
        var statistic = Render<Statistic>(p => p.Add(x => x.Title, "Keys allocated").Add(x => x.Value, "3"));

        Assert.Equal("Keys allocated", statistic.Find(".lc-statistic-title").TextContent);
        Assert.Equal("3", statistic.Find(".lc-statistic-value").TextContent.Trim());
    }
}
