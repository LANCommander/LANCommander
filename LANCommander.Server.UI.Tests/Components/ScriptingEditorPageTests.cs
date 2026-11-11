using Bunit;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using ModuleEdit = LANCommander.Server.UI.Pages.Scripting.Modules.Edit;
using SnippetEdit = LANCommander.Server.UI.Pages.Scripting.Snippets.Edit;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The snippet and module editors, laid out like the script editor dialog: the snippets or the
/// module's functions listed on the left with the open one marked, the open item's fields in the
/// rail beside what it can insert, and the unsaved state once something changes. Nothing here saves a
/// module; the snippets seeded are removed afterwards.
/// </summary>
[Collection("BUnit")]
public class ScriptingEditorPageTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private (string Group, string Open, string Other) SeedSnippets()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var scriptService = scope.ServiceProvider.GetRequiredService<ScriptService>();
        var group = "Page " + Guid.NewGuid().ToString("N");

        scriptService.SaveSnippet(new Snippet { Group = group, Name = "Open", Content = "Write-Host 'open'" });
        scriptService.SaveSnippet(new Snippet { Group = group, Name = "Other", Content = "Write-Host 'other'" });

        return (group, "Open", "Other");
    }

    private void DeleteSnippets(string group, params string[] names)
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        var scriptService = scope.ServiceProvider.GetRequiredService<ScriptService>();

        foreach (var name in names)
            scriptService.DeleteSnippet(group, name);
    }

    private IRenderedComponent<T> RenderEditor<T>(Action<ComponentParameterCollectionBuilder<T>> parameters) where T : Microsoft.AspNetCore.Components.IComponent
    {
        var cut = Render(parameters);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".script-editor-framed .script-editor-toolbar")), TimeSpan.FromSeconds(10));

        return cut;
    }

    [Fact]
    public void Snippet_ListsSnippetsByGroup_WithTheOpenOneMarked()
    {
        var (group, open, other) = SeedSnippets();

        try
        {
            var cut = RenderEditor<SnippetEdit>(p => p.Add(e => e.Group, group).Add(e => e.Name, open));

            var groupHeader = cut.FindAll(".script-editor-scripts .script-editor-group")
                .Single(g => g.QuerySelector(".script-editor-group-name")!.TextContent.Trim() == group);

            var names = groupHeader.QuerySelectorAll(".script-editor-script-name").Select(n => n.TextContent.Trim()).ToList();
            Assert.Equal([open, other], names);

            var current = groupHeader.QuerySelector(".script-editor-script-current");
            Assert.NotNull(current);
            Assert.Equal(open, current!.QuerySelector(".script-editor-script-name")!.TextContent.Trim());

            // The rail offers the other snippet to insert, but not the one open
            var chips = cut.FindAll(".script-editor-snippets .script-editor-chip").Select(c => c.TextContent.Trim()).ToList();
            Assert.Contains(other, chips);
            Assert.DoesNotContain(open, chips);
        }
        finally
        {
            DeleteSnippets(group, open, other);
        }
    }

    [Fact]
    public void Snippet_ShowsUnsavedChanges_WhenRenamed()
    {
        var (group, open, other) = SeedSnippets();

        try
        {
            var cut = RenderEditor<SnippetEdit>(p => p.Add(e => e.Group, group).Add(e => e.Name, open));

            Assert.Empty(cut.FindAll(".script-editor-unsaved"));

            var nameId = cut.FindAll(".script-editor-rail label.lc-form-item-label")
                .First(l => l.TextContent.Trim().StartsWith("Name"))
                .GetAttribute("for");

            cut.Find($"#{nameId}").Input("Renamed");

            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".script-editor-unsaved")));
            Assert.Contains("Unsaved changes", cut.Find(".script-editor-statusbar").TextContent);
        }
        finally
        {
            DeleteSnippets(group, open, other);
        }
    }

    [Fact]
    public void Module_New_ListsItsFunctionUnderPublic_AndEditsItInTheRail()
    {
        var cut = RenderEditor<ModuleEdit>(p => p.Add(e => e.Name, "Missing" + Guid.NewGuid().ToString("N")));

        var publicGroup = cut.FindAll(".script-editor-scripts .script-editor-group")
            .Single(g => g.QuerySelector(".script-editor-group-name")!.TextContent.Trim() == "Public");

        var current = publicGroup.QuerySelector(".script-editor-script-current .script-editor-script-name");
        Assert.NotNull(current);
        Assert.Equal("Get-Example", current!.TextContent.Trim());

        // The open function's card, and the module's own
        var kickers = cut.FindAll(".script-editor-rail .script-editor-card > .script-editor-kicker").Select(k => k.TextContent.Trim()).ToList();
        Assert.Contains("Function", kickers);
        Assert.Contains("Module", kickers);

        // The manifest is only listed once it is written by hand
        Assert.DoesNotContain(cut.FindAll(".script-editor-script-name"), n => n.TextContent.Trim() == "Manifest");
    }

    [Fact]
    public void Module_AddFunction_OpensAnUnnamedFunctionMarkedInvalid()
    {
        var cut = RenderEditor<ModuleEdit>(p => p.Add(e => e.Name, "Missing" + Guid.NewGuid().ToString("N")));

        cut.Find(".script-editor-panel-footer button").Click();

        cut.WaitForAssertion(() =>
        {
            var current = cut.Find(".script-editor-script-current");

            Assert.Equal("(unnamed)", current.QuerySelector(".script-editor-script-name")!.TextContent.Trim());
            Assert.Equal("Invalid", current.QuerySelector(".lc-tag")!.TextContent.Trim(), ignoreCase: true);
        });
    }
}
