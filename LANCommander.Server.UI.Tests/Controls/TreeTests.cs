using Bunit;
using LANCommander.Server.UI.Controls;

namespace LANCommander.Server.UI.Tests.Controls;

public class TreeTests : ControlsTestContext
{
    public sealed record Node(string Name, Node[]? Children = null);

    private static readonly Node[] Roots =
    [
        new("Welcome", [new("House Rules"), new("Schedule", [new("Saturday")])]),
        new("Servers"),
    ];

    private IRenderedComponent<Tree<Node>> RenderTree(Action<ComponentParameterCollectionBuilder<Tree<Node>>>? parameters = null) =>
        Render<Tree<Node>>(p =>
        {
            p.Add(x => x.Items, Roots)
                .Add(x => x.Children, n => n.Children)
                .Add(x => x.Key, n => n.Name)
                .Add(x => x.Text, n => n.Name);

            parameters?.Invoke(p);
        });

    private static List<string> Labels(IRenderedComponent<Tree<Node>> tree) =>
        tree.FindAll(".lc-tree-label").Select(l => l.TextContent.Trim()).ToList();

    [Fact]
    public void Collapsed_ShowsOnlyRoots()
    {
        Assert.Equal(["Welcome", "Servers"], Labels(RenderTree()));
    }

    [Fact]
    public void ExpandAll_ShowsEveryLevel()
    {
        var tree = RenderTree(p => p.Add(x => x.ExpandAll, true));

        Assert.Equal(["Welcome", "House Rules", "Schedule", "Saturday", "Servers"], Labels(tree));
    }

    [Fact]
    public void OnExpand_RunsBeforeChildrenAreShown_AndNotOnCollapse()
    {
        var expanded = new List<string>();
        IRenderedComponent<Tree<Node>>? tree = null;

        tree = RenderTree(p => p.Add(x => x.OnExpand, n =>
        {
            expanded.Add(n.Name);
            Assert.DoesNotContain("House Rules", Labels(tree!));
        }));

        tree.Find(".lc-tree-toggle").Click();
        tree.Find(".lc-tree-toggle").Click();

        Assert.Equal(["Welcome"], expanded);
    }

    [Fact]
    public void Toggle_ExpandsAndCollapses()
    {
        var tree = RenderTree();

        tree.Find(".lc-tree-toggle").Click();
        Assert.Contains("House Rules", Labels(tree));

        tree.Find(".lc-tree-toggle").Click();
        Assert.DoesNotContain("House Rules", Labels(tree));
    }

    [Fact]
    public void Clicking_SelectsItem()
    {
        Node? selected = null;
        var tree = RenderTree(p => p.Add(x => x.ValueChanged, n => selected = n));

        tree.FindAll(".lc-tree-node")[1].Click();

        Assert.Equal("Servers", selected?.Name);
        Assert.Contains("lc-tree-node-selected", tree.FindAll(".lc-tree-node")[1].ClassList);
    }

    [Fact]
    public void Dragging_OntoTheMiddleOfARow_DropsInside()
    {
        TreeDrop<Node>? drop = null;
        var tree = RenderTree(p => p.Add(x => x.Draggable, true).Add(x => x.OnDrop, d => drop = d));

        tree.FindAll(".lc-tree-node")[1].DragStart();

        var welcome = tree.FindAll(".lc-tree-node")[0];
        welcome.DragOver(new Microsoft.AspNetCore.Components.Web.DragEventArgs { OffsetY = 16 });
        // Each drag event re-renders the tree, so find the row again
        tree.FindAll(".lc-tree-node")[0].Drop();

        Assert.NotNull(drop);
        Assert.Equal("Servers", drop!.Item.Name);
        Assert.Equal("Welcome", drop.Target.Name);
        Assert.Equal(TreeDropPosition.Inside, drop.Position);
    }

    [Fact]
    public void Dragging_OntoTheTopEdge_DropsBefore()
    {
        TreeDrop<Node>? drop = null;
        var tree = RenderTree(p => p.Add(x => x.Draggable, true).Add(x => x.OnDrop, d => drop = d));

        tree.FindAll(".lc-tree-node")[1].DragStart();

        var welcome = tree.FindAll(".lc-tree-node")[0];
        welcome.DragOver(new Microsoft.AspNetCore.Components.Web.DragEventArgs { OffsetY = 2 });
        tree.FindAll(".lc-tree-node")[0].Drop();

        Assert.Equal(TreeDropPosition.Before, drop?.Position);
    }

    [Fact]
    public void Dragging_IntoOwnDescendant_IsIgnored()
    {
        TreeDrop<Node>? drop = null;
        var tree = RenderTree(p => p.Add(x => x.Draggable, true).Add(x => x.ExpandAll, true).Add(x => x.OnDrop, d => drop = d));

        tree.FindAll(".lc-tree-node")[0].DragStart();

        var saturday = tree.FindAll(".lc-tree-node")[3];
        saturday.DragOver(new Microsoft.AspNetCore.Components.Web.DragEventArgs { OffsetY = 16 });
        tree.FindAll(".lc-tree-node")[3].Drop();

        Assert.Null(drop);
    }

    [Fact]
    public void TextInput_Prefix_RendersAddon()
    {
        var input = Render<TextInput>(p => p.Add(x => x.Prefix, "/Pages/"));

        Assert.Equal("/Pages/", input.Find(".lc-input-group .lc-input-addon").TextContent);
        Assert.NotNull(input.Find(".lc-input-group input"));
    }

    [Fact]
    public void Button_Href_RendersStyledLink()
    {
        var button = Render<Button>(p => p.Add(x => x.Href, "/Games/1").Add(x => x.NewTab, true).Add(x => x.Primary, true).AddChildContent("Edit"));

        var link = button.Find("a.lc-button");

        Assert.Equal("/Games/1", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("rz-primary", link.ClassList);
    }

    [Fact]
    public void Button_Href_Disabled_HasNoHref()
    {
        var link = Render<Button>(p => p.Add(x => x.Href, "/Games/1").Add(x => x.Disabled, true).AddChildContent("Edit")).Find("a");

        Assert.Null(link.GetAttribute("href"));
        Assert.Equal("true", link.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void Checkable_ParentReflectsItsLeaves()
    {
        var saturday = Roots[0].Children![1].Children![0];

        var tree = RenderTree(p => p
            .Add(x => x.ExpandAll, true)
            .Add(x => x.Checkable, true)
            .Add(x => x.CheckedValues, [saturday]));

        var states = tree.FindAll(".lc-tree-checkbox").Select(b => b.GetAttribute("aria-checked")).ToList();

        // Welcome (partial), House Rules, Schedule (its one leaf is checked), Saturday, Servers
        Assert.Equal(["mixed", "false", "true", "true", "false"], states);
    }

    [Fact]
    public void Checkable_CheckingAParentChecksEveryLeaf_AndAgainUnchecksThem()
    {
        IEnumerable<Node> checkedNodes = [];

        var tree = RenderTree(p => p
            .Add(x => x.ExpandAll, true)
            .Add(x => x.Checkable, true)
            .Add(x => x.CheckedValuesChanged, v => checkedNodes = v));

        tree.FindAll(".lc-tree-checkbox")[0].Click();

        Assert.Equal(["House Rules", "Saturday"], checkedNodes.Select(n => n.Name).OrderBy(n => n));

        tree.FindAll(".lc-tree-checkbox")[0].Click();

        Assert.Empty(checkedNodes);
    }

    [Fact]
    public void Checkable_ClickingACheckbox_DoesNotSelectTheItem()
    {
        Node? selected = null;

        var tree = RenderTree(p => p.Add(x => x.Checkable, true).Add(x => x.ValueChanged, n => selected = n));

        tree.FindAll(".lc-tree-checkbox")[1].Click();

        Assert.Null(selected);
    }
}
