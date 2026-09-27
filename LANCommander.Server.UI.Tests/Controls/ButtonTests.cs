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
    [InlineData(null, true, "rz-variant-outlined", "rz-danger")]
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
    public void Loading_ShowsSpinnerInPlaceOfIcon_AndDisables()
    {
        var button = RenderButton(p => p.Add(x => x.Loading, true).Add(x => x.Icon, IconType.FloppyDisk));

        Assert.True(button.Find("button").HasAttribute("disabled"));
        Assert.Single(button.FindAll("svg.lc-icon"));
        Assert.Single(button.FindAll("svg.lc-icon-spin"));
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
    public void ClassAndBlock_AreApplied()
    {
        var classes = RenderButton(p => p.Add(x => x.Block, true).Add(x => x.Class, "extra")).Find("button").ClassList;

        Assert.Contains("lc-button-block", classes);
        Assert.Contains("extra", classes);
    }
}
