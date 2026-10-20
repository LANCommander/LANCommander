using System.Linq.Expressions;
using Bunit;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Tests.Controls;

public class ButtonTests : ControlsTestContext
{
    private IRenderedComponent<Button> RenderButton(Action<ComponentParameterCollectionBuilder<Button>> parameters) =>
        Render<Button>(p =>
        {
            p.AddChildContent("Save");
            parameters(p);
        });

    private static Expression<Func<Button, bool>> Flag(string name) => name switch
    {
        nameof(Button.Primary) => x => x.Primary,
        nameof(Button.Text) => x => x.Text,
        nameof(Button.Link) => x => x.Link,
        nameof(Button.Small) => x => x.Small,
        nameof(Button.Large) => x => x.Large,
        nameof(Button.ExtraSmall) => x => x.ExtraSmall,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Fact]
    public void Default_IsOutlinedBase()
    {
        var classes = RenderButton(_ => { }).Find("button").ClassList;

        Assert.Contains("rz-variant-outlined", classes);
        Assert.Contains("rz-base", classes);
        Assert.Contains("lc-button", classes);
    }

    [Theory]
    [InlineData(nameof(Button.Primary), false, "rz-variant-filled", "rz-primary")]
    [InlineData(nameof(Button.Primary), true, "rz-variant-filled", "rz-danger")]
    [InlineData(nameof(Button.Text), false, "rz-variant-text", "rz-base")]
    [InlineData(nameof(Button.Text), true, "rz-variant-text", "rz-danger")]
    [InlineData(nameof(Button.Link), false, "rz-variant-text", "rz-primary")]
    [InlineData(null, true, "rz-variant-outlined", "rz-base")]
    public void VariantFlags_MapToStyle(string? variant, bool danger, string expectedVariant, string expectedStyle)
    {
        var classes = RenderButton(p =>
        {
            if (variant != null)
                p.Add(Flag(variant), true);

            p.Add(x => x.Danger, danger);
        }).Find("button").ClassList;

        Assert.Contains(expectedVariant, classes);
        Assert.Contains(expectedStyle, classes);
    }

    [Theory]
    [InlineData(nameof(Button.Small), "rz-button-sm")]
    [InlineData(nameof(Button.ExtraSmall), "rz-button-xs")]
    [InlineData(nameof(Button.Large), "rz-button-lg")]
    [InlineData(null, "rz-button-md")]
    public void SizeFlags_MapToSize(string? size, string expected)
    {
        var classes = RenderButton(p =>
        {
            if (size != null)
                p.Add(Flag(size), true);
        }).Find("button").ClassList;

        Assert.Contains(expected, classes);
    }

    [Fact]
    public void Click_InvokesOnClick()
    {
        var clicks = 0;
        var button = RenderButton(p => p.Add(x => x.OnClick, () => clicks++));

        button.Find("button").Click();

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void Disabled_DisablesButton()
    {
        var button = RenderButton(p => p.Add(x => x.Disabled, true));

        Assert.True(button.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Danger_Alone_IsTheSecondaryButtonWithDangerText()
    {
        var classes = RenderButton(p => p.Add(x => x.Danger, true)).Find("button").ClassList;

        Assert.Contains("lc-button-danger", classes);
        Assert.DoesNotContain("rz-danger", classes);
    }

    [Fact]
    public void Danger_WithPrimary_StaysFilled()
    {
        var classes = RenderButton(p => p.Add(x => x.Danger, true).Add(x => x.Primary, true)).Find("button").ClassList;

        Assert.Contains("rz-danger", classes);
        Assert.DoesNotContain("lc-button-danger", classes);
    }

    [Fact]
    public void Loading_ShowsRingInPlaceOfIcon_KeepsLabel_AndDisables()
    {
        var button = RenderButton(p => p.Add(x => x.Loading, true).Add(x => x.Icon, IconType.FloppyDisk));

        var element = button.Find("button");

        Assert.True(element.HasAttribute("disabled"));
        Assert.Contains("lc-button-loading", element.ClassList);
        Assert.Single(button.FindAll(".lc-spinner"));
        Assert.Empty(button.FindAll("svg.lc-icon"));
        Assert.Equal("Save", button.Find(".lc-button-text").TextContent);
    }

    [Fact]
    public void Submit_SetsSubmitType()
    {
        Assert.Equal("submit", RenderButton(p => p.Add(x => x.Submit, true)).Find("button").GetAttribute("type"));
        Assert.Equal("button", RenderButton(_ => { }).Find("button").GetAttribute("type"));
    }

    [Fact]
    public void IconWithoutContent_IsIconOnly()
    {
        var button = Render<Button>(p => p.Add(x => x.Icon, IconType.Trash).AddUnmatched("title", "Delete"));

        Assert.Contains("rz-button-icon-only", button.Find("button").ClassList);
        Assert.Equal("Delete", button.Find("button").GetAttribute("title"));
        Assert.Empty(button.FindAll(".lc-button-text"));
    }

    [Fact]
    public void IconOnly_KeepsSquareClass_AtEverySize()
    {
        foreach (var (apply, size) in new (Action<ComponentParameterCollectionBuilder<Button>>, string)[]
                 {
                     (_ => { }, "rz-button-md"),
                     (p => p.Add(x => x.Small, true), "rz-button-sm"),
                     (p => p.Add(x => x.ExtraSmall, true), "rz-button-xs"),
                     (p => p.Add(x => x.Large, true), "rz-button-lg"),
                 })
        {
            var classes = Render<Button>(p =>
            {
                p.Add(x => x.Icon, IconType.Plus);
                p.AddUnmatched("title", "Add");
                apply(p);
            }).Find("button").ClassList;

            Assert.Contains("rz-button-icon-only", classes);
            Assert.Contains(size, classes);
        }
    }

    [Fact]
    public void ClassAndBlock_AreApplied()
    {
        var classes = RenderButton(p => p.Add(x => x.Block, true).Add(x => x.Class, "extra")).Find("button").ClassList;

        Assert.Contains("lc-button-block", classes);
        Assert.Contains("extra", classes);
    }
}
