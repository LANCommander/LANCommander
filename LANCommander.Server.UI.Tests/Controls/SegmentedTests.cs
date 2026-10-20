using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class SegmentedTests : ControlsTestContext
{
    private static readonly SelectItem<string>[] Modes =
    [
        new("lookup", "Look up metadata"),
        new("manual", "Enter manually"),
    ];

    [Fact]
    public void MarksTheChosenOption()
    {
        var control = Render<Segmented<string>>(p => p.Add(x => x.Items, Modes).Add(x => x.Value, "manual"));

        var options = control.FindAll("[role=radio]");

        Assert.Equal("false", options[0].GetAttribute("aria-checked"));
        Assert.Equal("true", options[1].GetAttribute("aria-checked"));
        Assert.Contains("lc-segmented-option-selected", options[1].ClassList);
    }

    [Fact]
    public void Choosing_RaisesValueChanged()
    {
        string? chosen = null;

        var control = Render<Segmented<string>>(p => p
            .Add(x => x.Items, Modes)
            .Add(x => x.Value, "lookup")
            .Add(x => x.ValueChanged, value => chosen = value));

        control.FindAll("[role=radio]")[1].Click();

        Assert.Equal("manual", chosen);
    }

    [Fact]
    public void Tag_OnClose_AddsARemoveButton()
    {
        var closed = false;

        var tag = Render<Tag>(p => p
            .AddChildContent("Action")
            .Add(x => x.CloseLabel, "Remove Action")
            .Add(x => x.OnClose, () => closed = true));

        tag.Find("button.lc-tag-close[aria-label='Remove Action']").Click();

        Assert.True(closed);
    }

    [Fact]
    public void Tag_WithoutOnClose_HasNoRemoveButton()
    {
        var tag = Render<Tag>(p => p.AddChildContent("Action"));

        Assert.Empty(tag.FindAll(".lc-tag-close"));
    }
}
