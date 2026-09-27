using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class DisplayTests : ControlsTestContext
{
    [Fact]
    public void Typography_MapsFlags()
    {
        var span = Render<Typography>(p => p
            .AddChildContent("hello")
            .Add(x => x.Secondary, true)
            .Add(x => x.Strong, true)
            .Add(x => x.Danger, true)).Find("span");

        Assert.Contains("lc-typography-secondary", span.ClassList);
        Assert.Contains("lc-typography-strong", span.ClassList);
        Assert.Contains("lc-typography-danger", span.ClassList);
        Assert.Equal("hello", span.TextContent);
    }

    [Fact]
    public void Typography_Code_RendersCodeElement()
    {
        var text = Render<Typography>(p => p.AddChildContent("C:\\Games").Add(x => x.Code, true));

        Assert.Equal("C:\\Games", text.Find("code.lc-typography-code").TextContent);
    }

    [Theory]
    [InlineData(true, false, "lc-tag-success")]
    [InlineData(false, true, "lc-tag-danger")]
    public void Tag_StatusFlags(bool success, bool danger, string expected)
    {
        var span = Render<Tag>(p => p.AddChildContent("x").Add(x => x.Success, success).Add(x => x.Danger, danger)).Find("span");

        Assert.Contains(expected, span.ClassList);
    }

    [Fact]
    public void Tag_CustomColor_OverridesStatus()
    {
        var span = Render<Tag>(p => p.AddChildContent("x").Add(x => x.Success, true).Add(x => x.Color, "#ff00ff")).Find("span");

        Assert.Contains("lc-tag-custom", span.ClassList);
        Assert.DoesNotContain("lc-tag-success", span.ClassList);
        Assert.Equal("--lc-tag-color: #ff00ff", span.GetAttribute("style"));
    }

    [Fact]
    public void Tag_Clickable_IsAButton()
    {
        var clicked = false;
        var span = Render<Tag>(p => p.AddChildContent("x").Add(x => x.OnClick, () => clicked = true)).Find("span");

        Assert.Equal("button", span.GetAttribute("role"));
        Assert.Contains("lc-tag-clickable", span.ClassList);

        span.Click();

        Assert.True(clicked);
    }

    [Fact]
    public void Tag_NotClickable_HasNoButtonRole()
    {
        var span = Render<Tag>(p => p.AddChildContent("x")).Find("span");

        Assert.Null(span.GetAttribute("role"));
        Assert.Null(span.GetAttribute("tabindex"));
    }

    [Fact]
    public void Badge_ShowsDotAndText()
    {
        var badge = Render<Badge>(p => p.AddChildContent("Running").Add(x => x.Success, true));

        Assert.Contains("lc-badge-success", badge.Find(".lc-badge").ClassList);
        Assert.Single(badge.FindAll(".lc-badge-dot"));
        Assert.Equal("Running", badge.Find(".lc-badge-text").TextContent);
    }

    [Fact]
    public void Badge_Processing_IsInfoAndPulses()
    {
        var classes = Render<Badge>(p => p.Add(x => x.Processing, true)).Find(".lc-badge").ClassList;

        Assert.Contains("lc-badge-info", classes);
        Assert.Contains("lc-badge-processing", classes);
    }

    [Fact]
    public void Divider_WithText_RendersLabel()
    {
        var divider = Render<Divider>(p => p.AddChildContent("Security"));

        Assert.Contains("lc-divider-with-text", divider.Find("[role=separator]").ClassList);
        Assert.Equal("Security", divider.Find(".lc-divider-text").TextContent);
    }

    [Fact]
    public void Divider_Vertical_IsInlineSeparator()
    {
        var separator = Render<Divider>(p => p.Add(x => x.Vertical, true)).Find("[role=separator]");

        Assert.Equal("span", separator.TagName.ToLowerInvariant());
        Assert.Equal("vertical", separator.GetAttribute("aria-orientation"));
    }

    [Fact]
    public void Card_RendersTitleExtraAndBody()
    {
        var card = Render<Card>(p => p
            .Add(x => x.Title, "Servers")
            .Add(x => x.Extra, "<button>Add</button>")
            .AddChildContent("<p>body</p>"));

        Assert.Equal("Servers", card.Find(".lc-card-title").TextContent.Trim());
        Assert.NotNull(card.Find(".lc-card-extra button"));
        Assert.Equal("body", card.Find(".lc-card-body p").TextContent);
    }

    [Fact]
    public void Card_WithoutTitleOrExtra_HasNoHeader()
    {
        var card = Render<Card>(p => p.AddChildContent("body"));

        Assert.Empty(card.FindAll(".lc-card-header"));
    }

    [Fact]
    public void Card_Loading_CoversBody()
    {
        var card = Render<Card>(p => p.AddChildContent("body").Add(x => x.Loading, true));

        Assert.Single(card.FindAll(".lc-card-body .lc-spin-overlay"));
    }

    [Fact]
    public void Avatar_WithoutPicture_ShowsInitial()
    {
        var avatar = Render<Avatar>(p => p.Add(x => x.Name, "jordan"));

        Assert.Equal("J", avatar.Find(".lc-avatar-initial").TextContent);
        Assert.Equal("jordan", avatar.Find(".lc-avatar").GetAttribute("aria-label"));
    }

    [Fact]
    public void Avatar_WithoutPictureOrName_ShowsIcon()
    {
        var avatar = Render<Avatar>(p => p.Add(x => x.Icon, IconType.UsersThree));

        Assert.NotNull(avatar.Find(".lc-avatar svg"));
        Assert.Empty(avatar.FindAll(".lc-avatar-initial"));
    }

    [Fact]
    public void Avatar_WithPicture_ShowsImage_AndSizeFlags()
    {
        var avatar = Render<Avatar>(p => p.Add(x => x.Src, "/avatar.png").Add(x => x.Name, "alex").Add(x => x.Large, true).Add(x => x.Square, true));

        Assert.Equal("/avatar.png", avatar.Find("img").GetAttribute("src"));
        Assert.Contains("lc-avatar-large", avatar.Find(".lc-avatar").ClassList);
        Assert.Contains("lc-avatar-square", avatar.Find(".lc-avatar").ClassList);
    }

    [Fact]
    public void AvatarGroup_ShowsOverflowCount()
    {
        var group = Render<AvatarGroup>(p => p.Add(x => x.Overflow, 3).AddChildContent<Avatar>(a => a.Add(x => x.Name, "sam")));

        Assert.Equal("+3", group.Find(".lc-avatar-overflow").TextContent);
    }
}
