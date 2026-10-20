using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class FormExtrasTests : ControlsTestContext
{
    private const long MiB = 1024 * 1024;

    [Fact]
    public void ByteSizeInput_ShowsTheLargestWholeUnit()
    {
        var input = Render<ByteSizeInput>(p => p.Add(x => x.Value, 50 * MiB));

        Assert.Equal("50", input.Find(".rz-numeric input").GetAttribute("value"));
        Assert.Contains("MiB", input.Find(".lc-byte-size-unit").TextContent);
    }

    [Fact]
    public void ByteSizeInput_ChangingTheAmount_MultipliesByTheUnit()
    {
        long value = 0;

        var input = Render<ByteSizeInput>(p => p.Add(x => x.Value, 50 * MiB).Add(x => x.ValueChanged, v => value = v));

        input.Find(".rz-numeric input").Input("8");

        Assert.Equal(8 * MiB, value);
    }

    [Fact]
    public void ByteSizeInput_ClampsToMaximum_AndOffersNoLargerUnits()
    {
        long value = 0;

        var input = Render<ByteSizeInput>(p => p
            .Add(x => x.Value, 10 * MiB)
            .Add(x => x.Maximum, 100 * MiB)
            .Add(x => x.ValueChanged, v => value = v));

        input.Find(".rz-numeric input").Input("500");

        Assert.Equal(100 * MiB, value);
    }

    [Fact]
    public void InputGroup_WrapsChildren()
    {
        var group = Render<InputGroup>(p => p.Add(x => x.Block, true).AddChildContent<InputAddon>(a => a.AddChildContent("x")));

        Assert.Contains("lc-input-group-block", group.Find(".lc-input-group").ClassList);
        Assert.Equal("x", group.Find(".lc-input-group > .lc-input-addon").TextContent.Trim());
    }

    [Fact]
    public void AutoComplete_AcceptsFreeText()
    {
        string? value = null;

        var input = Render<AutoComplete>(p => p
            .Add(x => x.Options, ["preferred_username", "email"])
            .Add(x => x.ValueChanged, v => value = v));

        input.Find("input").Change("custom_claim");

        Assert.Equal("custom_claim", value);
    }

    public sealed record Role(Guid Id, string Name);

    private static readonly Role[] Roles =
    [
        new(Guid.NewGuid(), "Administrator"),
        new(Guid.NewGuid(), "Players"),
        new(Guid.NewGuid(), "Guests"),
    ];

    [Fact]
    public void Transfer_SplitsItemsByValues_MatchingOnId()
    {
        // Chosen values loaded separately from the items: equal Ids, different instances
        var chosen = new[] { Roles[1] with { } };

        var transfer = Render<Transfer<Role>>(p => p
            .Add(x => x.Items, Roles)
            .Add(x => x.Values, chosen)
            .Add(x => x.Text, r => r.Name));

        var lists = transfer.FindAll(".rz-picklist-source-wrapper, .rz-picklist-target-wrapper");

        Assert.Contains("Administrator", lists[0].TextContent);
        Assert.DoesNotContain("Players", lists[0].TextContent);
        Assert.Contains("Players", lists[1].TextContent);
    }

    [Fact]
    public void Transfer_MovingAll_ReportsEveryItem()
    {
        IEnumerable<Role>? values = null;

        var transfer = Render<Transfer<Role>>(p => p
            .Add(x => x.Items, Roles)
            .Add(x => x.Text, r => r.Name)
            .Add(x => x.ValuesChanged, v => values = v));

        transfer.Find(".rz-picklist-buttons button[title='Add all']").Click();

        Assert.Equal(Roles.Select(r => r.Name), values?.Select(r => r.Name));
    }
}
