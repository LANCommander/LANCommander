using Bunit;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>The variants and controls added to match the exported design boards.</summary>
public class DesignControlTests : ControlsTestContext
{
    [Theory]
    [InlineData(nameof(Tag.Chip), "lc-tag-chip")]
    [InlineData(nameof(Tag.Caps), "lc-tag-caps")]
    public void Tag_Variants_AddTheirClass(string variant, string expected)
    {
        var classes = Render<Tag>(p =>
        {
            p.AddChildContent("Cover");

            if (variant == nameof(Tag.Chip))
                p.Add(x => x.Chip, true);
            else
                p.Add(x => x.Caps, true);
        }).Find(".lc-tag").ClassList;

        Assert.Contains(expected, classes);
    }

    [Fact]
    public void Tag_WarningChip_CombinesChipAndStatus()
    {
        var classes = Render<Tag>(p => p.AddChildContent("Has archive").Add(x => x.Chip, true).Add(x => x.Warning, true)).Find(".lc-tag").ClassList;

        Assert.Contains("lc-tag-chip", classes);
        Assert.Contains("lc-tag-warning", classes);
    }

    [Fact]
    public void Tag_SmallCaps_WithIcon()
    {
        var tag = Render<Tag>(p => p.AddChildContent("Archive").Add(x => x.Caps, true).Add(x => x.Small, true).Add(x => x.Icon, IconType.Archive));

        Assert.Contains("lc-tag-caps-small", tag.Find(".lc-tag").ClassList);
        Assert.NotNull(tag.Find(".lc-tag svg.lc-icon"));
    }

    [Fact]
    public void Tag_Small_WithoutCaps_ChangesNothing()
    {
        var classes = Render<Tag>(p => p.AddChildContent("x").Add(x => x.Small, true)).Find(".lc-tag").ClassList;

        Assert.DoesNotContain("lc-tag-caps-small", classes);
    }

    [Fact]
    public void Badge_Count_RendersAPillInsteadOfTheDot()
    {
        var badge = Render<Badge>(p => p.Add(x => x.Count, 12));

        var pill = badge.Find(".lc-badge-count");

        Assert.Equal("12", pill.TextContent);
        Assert.Empty(badge.FindAll(".lc-badge-dot"));
        Assert.DoesNotContain("lc-badge-count-neutral", pill.ClassList);
    }

    [Fact]
    public void Badge_NeutralCount_TakesText()
    {
        var pill = Render<Badge>(p => p.Add(x => x.Count, "9 / 24").Add(x => x.Neutral, true)).Find(".lc-badge-count");

        Assert.Equal("9 / 24", pill.TextContent);
        Assert.Contains("lc-badge-count-neutral", pill.ClassList);
    }

    [Fact]
    public void Statistic_ShowsDeltaColouredByStatus_AndUsage()
    {
        var statistic = Render<Statistic>(p => p
            .Add(x => x.Title, "Archive storage")
            .Add(x => x.Value, "1.24")
            .Add(x => x.Delta, "TB of 4 TB")
            .Add(x => x.Warning, true)
            .Add(x => x.Usage, 31));

        var delta = statistic.Find(".lc-statistic-delta");

        Assert.Equal("TB of 4 TB", delta.TextContent);
        Assert.Contains("lc-statistic-delta-warning", delta.ClassList);
        Assert.Equal("31", statistic.Find(".lc-statistic-usage").GetAttribute("aria-valuenow"));
        Assert.Contains("width: 31%", statistic.Find(".lc-statistic-usage-value").GetAttribute("style"));
    }

    [Fact]
    public void Statistic_WithoutDeltaOrUsage_RendersNeither()
    {
        var statistic = Render<Statistic>(p => p.Add(x => x.Title, "Players online").Add(x => x.Value, "14"));

        Assert.Empty(statistic.FindAll(".lc-statistic-delta"));
        Assert.Empty(statistic.FindAll(".lc-statistic-usage"));
    }

    [Fact]
    public void Statistic_UsageIsClamped()
    {
        var statistic = Render<Statistic>(p => p.Add(x => x.Title, "Storage").Add(x => x.Usage, 140));

        Assert.Equal("100", statistic.Find(".lc-statistic-usage").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Empty_Title_RendersHeading_WithoutDefaultDescription()
    {
        var empty = Render<Empty>(p => p.Add(x => x.Title, "No games yet"));

        Assert.Equal("No games yet", empty.Find(".lc-empty-title").TextContent);
        Assert.Empty(empty.FindAll(".lc-empty-description"));
    }

    [Fact]
    public void Empty_TitleAndDescription()
    {
        var empty = Render<Empty>(p => p.Add(x => x.Title, "No games yet").Add(x => x.Description, "Add a title to get started."));

        Assert.Equal("No games yet", empty.Find(".lc-empty-title").TextContent);
        Assert.Equal("Add a title to get started.", empty.Find(".lc-empty-description").TextContent.Trim());
    }

    [Fact]
    public void Alert_Action_RendersUnderTheMessage()
    {
        var alert = Render<Alert>(p => p
            .Add(x => x.Danger, true)
            .AddChildContent("IGDB credentials were rejected.")
            .Add(x => x.Action, "<a href='/Settings'>Open integration settings</a>"));

        Assert.Equal("Open integration settings", alert.Find(".lc-alert-content .lc-alert-action a").TextContent);
    }

    [Fact]
    public void Spinner_IsDecorativeWithoutLabel()
    {
        var spinner = Render<Spinner>().Find(".lc-spinner");

        Assert.Equal("true", spinner.GetAttribute("aria-hidden"));
        Assert.Null(spinner.GetAttribute("role"));
    }

    [Fact]
    public void Spinner_WithLabel_IsAStatus()
    {
        var spinner = Render<Spinner>(p => p.Add(x => x.Label, "Loading").Add(x => x.Large, true));

        var root = spinner.Find(".lc-spinner");

        Assert.Equal("status", root.GetAttribute("role"));
        Assert.Contains("lc-spinner-large", root.ClassList);
        Assert.Equal("Loading", spinner.Find(".lc-visually-hidden").TextContent);
    }

    [Fact]
    public void Spin_UsesTheRing()
    {
        var spin = Render<Spin>(p => p.Add(x => x.Tip, "Fetching archives…"));

        Assert.Single(spin.FindAll(".lc-spin .lc-spinner"));
        Assert.Empty(spin.FindAll(".rz-progressbar-circular"));
    }

    [Fact]
    public void Skeleton_DrawsRowsCyclingTheWidths()
    {
        var bars = Render<Skeleton>(p => p.Add(x => x.Rows, 4).Add(x => x.Widths, new[] { "70%", "40%" })).FindAll(".lc-skeleton-bar");

        Assert.Equal(4, bars.Count);
        Assert.Equal(["width: 70%", "width: 40%", "width: 70%", "width: 40%"], bars.Select(b => b.GetAttribute("style")));
    }

    [Theory]
    [InlineData("Alex Mercer", "AM")]
    [InlineData("  priya  ", "P")]
    [InlineData("Jo van der Berg", "JB")]
    public void Avatar_ShowsInitials(string name, string expected)
    {
        Assert.Equal(expected, Render<Avatar>(p => p.Add(x => x.Name, name)).Find(".lc-avatar-initial").TextContent);
    }

    [Fact]
    public void Avatar_ToneFollowsTheColorKey_NotTheName()
    {
        string Tone(string name, string key) => Render<Avatar>(p => p.Add(x => x.Name, name).Add(x => x.ColorKey, key))
            .Find(".lc-avatar").ClassList.Single(c => c.StartsWith("lc-avatar-tone-"));

        var tone = Tone("Alex Mercer", "user-1");

        Assert.Equal(tone, Tone("Alex M.", "user-1"));
        Assert.Matches("^lc-avatar-tone-[0-3]$", tone);
    }

    [Fact]
    public void Avatar_WithoutNameOrKey_HasNoTone()
    {
        var classes = Render<Avatar>().Find(".lc-avatar").ClassList;

        Assert.DoesNotContain(classes, c => c.StartsWith("lc-avatar-tone-"));
    }

    [Fact]
    public void Card_Panel_WithCountSubtitleAndFooter()
    {
        var card = Render<Card>(p => p
            .Add(x => x.Title, "Active sessions")
            .Add(x => x.Panel, true)
            .Add(x => x.Count, 14)
            .Add(x => x.Footer, "<a href='/Sessions'>See all sessions</a>")
            .AddChildContent("<p>rows</p>"));

        Assert.Contains("lc-card-panel", card.Find(".lc-card").ClassList);
        Assert.Equal("14", card.Find(".lc-card-header .lc-badge-count-neutral").TextContent);
        Assert.NotNull(card.Find(".lc-card-footer a"));
    }

    [Fact]
    public void Card_Subtitle_SitsInTheHeader()
    {
        var card = Render<Card>(p => p.Add(x => x.Title, "Playtime by title").Add(x => x.Subtitle, "last 7 days"));

        Assert.Equal("last 7 days", card.Find(".lc-card-header .lc-card-subtitle").TextContent);
    }

    [Fact]
    public void PageHeader_BreadcrumbTagsAndSubtitleTemplate()
    {
        var header = Render<PageHeader>(p => p
            .Add(x => x.Title, "Arena Blitz")
            .Add(x => x.Breadcrumb, "<nav class='crumbs'>Games</nav>")
            .Add(x => x.Tags, "<span class='tag'>Published</span>")
            .Add(x => x.SubtitleTemplate, "<span class='version'>v1.2 · 4.70 GB</span>"));

        var heading = header.Find(".lc-page-header-heading");

        Assert.NotNull(heading.QuerySelector(".lc-page-header-breadcrumb .crumbs"));
        Assert.NotNull(heading.QuerySelector(".lc-page-header-title-row .lc-page-header-tags .tag"));
        Assert.NotNull(heading.QuerySelector(".lc-page-header-subtitle .version"));
    }

    [Fact]
    public void BreadcrumbItem_Mono()
    {
        var item = Render<BreadcrumbItem>(p => p.Add(x => x.Mono, true).AddChildContent("ns2-338.4.zip")).Find("li");

        Assert.Contains("lc-breadcrumb-item-mono", item.ClassList);
    }

    [Fact]
    public void Segmented_DialogVariant_WithIcons()
    {
        var items = new[]
        {
            new SelectItem<string>("lookup", "Look up metadata", Icon: IconType.MagnifyingGlass),
            new SelectItem<string>("manual", "Enter manually", Icon: IconType.PencilSimple),
        };

        var control = Render<Segmented<string>>(p => p.Add(x => x.Items, items).Add(x => x.Value, "lookup").Add(x => x.Dialog, true));

        var root = control.Find(".lc-segmented");

        Assert.Contains("lc-segmented-dialog", root.ClassList);
        Assert.Contains("lc-segmented-block", root.ClassList);
        Assert.Equal(2, control.FindAll(".lc-segmented-option svg.lc-segmented-icon").Count);
        Assert.Equal("Look up metadata", control.Find(".lc-segmented-label").TextContent);
    }

    [Fact]
    public void Segmented_IconOnly_LabelsTheOption()
    {
        var items = new[] { new SelectItem<string>("list", "List", Icon: IconType.List) };

        var option = Render<Segmented<string>>(p => p.Add(x => x.Items, items).Add(x => x.IconOnly, true)).Find(".lc-segmented-option");

        Assert.Equal("List", option.GetAttribute("aria-label"));
        Assert.Null(option.QuerySelector(".lc-segmented-label"));
    }
}

/// <summary>A dialog that puts its own, state-dependent content in the title bar.</summary>
public sealed class TitledDialog : Dialog<string>
{
    public static bool Dirty { get; set; }

    protected override RenderFragment? TitleContent => Dirty ? builder => builder.AddMarkupContent(0, "<span class=\"dirty\">• unsaved</span>") : null;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "make-dirty");
        builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => Dirty = true));
        builder.AddContent(3, "Edit");
        builder.CloseElement();
    }
}

/// <summary>Dialog title content and notification actions, which render through the ComponentHost.</summary>
public class DesignServiceTests : ControlsTestContext
{
    private readonly IRenderedComponent<ComponentHost> _host;

    public DesignServiceTests()
    {
        _host = Render<ComponentHost>();

        TitledDialog.Dirty = false;
    }

    private T Service<T>() where T : notnull => Services.GetRequiredService<T>();

    [Fact]
    public void Dialog_TitleContentAndSubtitle_RenderBesideTheTitle()
    {
        _ = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Download Cover", new EchoOptions("hello"), new DialogSettings
        {
            TitleContent = builder => builder.AddMarkupContent(0, "<span class=\"chip\">COVER</span>"),
            Subtitle = "Natural Selection 2",
        });

        _host.WaitForAssertion(() =>
        {
            var title = _host.Find(".lc-dialog-title");

            Assert.Equal("Download Cover", title.QuerySelector(".lc-dialog-title-text")!.TextContent);
            Assert.Equal("COVER", title.QuerySelector(".chip")!.TextContent);
            Assert.Equal("Natural Selection 2", title.QuerySelector(".lc-dialog-subtitle")!.TextContent);
        });
    }

    [Fact]
    public void Dialog_OwnTitleContent_FollowsItsState()
    {
        _ = Service<DialogService>().OpenAsync<TitledDialog, string>("Edit Script");

        _host.WaitForAssertion(() => Assert.Empty(_host.FindAll(".lc-dialog-title .dirty")));

        _host.Find("button.make-dirty").Click();

        _host.WaitForAssertion(() => Assert.Equal("• unsaved", _host.Find(".lc-dialog-title .dirty").TextContent));
    }

    [Fact]
    public void Dialog_Picker_AddsWindowClasses_AndFooterNote()
    {
        _ = Service<DialogService>().OpenAsync<EchoDialog, EchoOptions, string>("Edit Script", new EchoOptions("hello"), new DialogSettings
        {
            Picker = true,
            CompactFooter = true,
            FooterNote = "Saving replaces the stored script.",
        });

        _host.WaitForAssertion(() =>
        {
            var window = _host.Find(".lc-dialog-window");

            Assert.Contains("lc-dialog-picker", window.ClassList);
            Assert.Contains("lc-dialog-picker-compact", window.ClassList);
            Assert.Equal("Saving replaces the stored script.", _host.Find(".lc-dialog-footer .lc-dialog-footer-note").TextContent);
        });
    }

    [Fact]
    public void Notification_ActionAndMeta_RenderUnderTheDetail()
    {
        var ran = false;

        Service<NotificationService>().Error("Repack failed", "Checksum mismatch on pak0.pk3.",
            new NotificationAction("Retry", OnClick: () =>
            {
                ran = true;
                return Task.CompletedTask;
            }),
            meta: "job 4f21c8 · 21:12");

        _host.WaitForAssertion(() => Assert.Equal("job 4f21c8 · 21:12", _host.Find(".lc-notification-meta").TextContent));

        _host.Find("button.lc-notification-action").Click();

        Assert.True(ran);
        _host.WaitForAssertion(() => Assert.DoesNotContain("Repack failed", _host.Markup));
    }

    [Fact]
    public void Notification_HrefAction_IsALink()
    {
        Service<NotificationService>().Warning("Metadata partly missing", "Soldat 2 has no logo artwork.", new NotificationAction("Open media editor", Href: "/Games/1/Media"));

        _host.WaitForAssertion(() => Assert.Equal("/Games/1/Media", _host.Find("a.lc-notification-action").GetAttribute("href")));
    }
}
