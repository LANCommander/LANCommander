using Bunit;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using TagsIndex = LANCommander.Server.UI.Pages.Metadata.Tags.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the metadata (Tags) management page. Replaces the flaky
/// Playwright <c>MetadataTests</c> CRUD flows. Rendering and the add-via-modal flow run
/// synchronously in-process, removing the SignalR circuit races that made the modal +
/// data-table-reload Playwright tests unreliable.
/// </summary>
[Collection("BUnit")]
public class MetadataComponentTests : BUnitTestContext
{
    public MetadataComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private async Task ClearTagsAsync()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var tagService = scope.ServiceProvider.GetRequiredService<TagService>();
        foreach (var tag in await tagService.GetAsync())
            await tagService.DeleteAsync(tag);
    }

    [Fact]
    public async Task Tags_ShowsAddButton_AndEmptyState()
    {
        await ClearTagsAsync();

        var cut = RenderComponent<TagsIndex>();

        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Add Tag", StringComparison.OrdinalIgnoreCase));

        // The empty DataTable renders AntDesign's "No Data" placeholder once the async load
        // completes.
        cut.WaitForAssertion(
            () => Assert.Contains("No Data", cut.Markup, StringComparison.OrdinalIgnoreCase),
            timeout: TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Tags_CanAddTag()
    {
        await ClearTagsAsync();

        var cut = RenderComponent<TagsIndex>();

        // Open the "New Tag" modal.
        var addButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Add Tag", StringComparison.OrdinalIgnoreCase));
        addButton.Click();

        // Fill in the tag name inside the modal and confirm. AntDesign's Input commits its
        // bound value on the change event, so dispatch both input and change.
        var input = cut.WaitForElement(".ant-modal input", timeout: TimeSpan.FromSeconds(5));
        input.Input("Action");
        input.Change("Action");

        var okButton = cut.FindAll(".ant-modal button")
            .First(b => b.TextContent.Trim().Equals("OK", StringComparison.OrdinalIgnoreCase));
        okButton.Click();

        // The new tag is persisted and the data table reloads to show it.
        cut.WaitForAssertion(
            () => Assert.Contains("Action", cut.Markup),
            timeout: TimeSpan.FromSeconds(10));

        // Verify persistence at the service layer.
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var tagService = scope.ServiceProvider.GetRequiredService<TagService>();
        Assert.Contains(await tagService.GetAsync(), t => t.Name == "Action");
    }
}
