using System.Text.Json;
using Bunit;
using LANCommander.Server.Data;
using LANCommander.SDK.Events;
using LANCommander.Server.Services.Events;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Tests.Controls;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebhooksPage = LANCommander.Server.UI.Pages.Settings.Integrations.Webhooks.Index;
using WebhookEditor = LANCommander.Server.UI.Pages.Settings.Integrations.Webhooks.General;
using WebhookDeliveries = LANCommander.Server.UI.Pages.Settings.Integrations.Webhooks.Deliveries;

namespace LANCommander.Server.UI.Tests.Components;

public sealed class WebhookSettingsComponentTests : ControlsTestContext
{
    private readonly SqliteConnection _connection;
    private const string Secret = "webhook-preview-test-secret-at-least-32-characters";
    private WebhookService Webhooks => Services.GetRequiredService<WebhookService>();
    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    public WebhookSettingsComponentTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Services.AddDbContextFactory<DatabaseContext>(options => options.UseSqlite(_connection));
        Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        Services.AddScoped<WebhookService>();
        using var context = Services.GetRequiredService<IDbContextFactory<DatabaseContext>>().CreateDbContext();
        context.Database.EnsureCreated();
    }

    private Task<Guid> CreateAsync(bool enabled = true, string? secret = null) =>
        Webhooks.SaveAsync(null, new WebhookEdit
        {
            Name = "Example",
            Endpoint = "https://example.com/events",
            Secret = secret,
            Enabled = enabled,
            EventTypes = [ServerEventTypes.GameCreated],
        });

    [Fact]
    public async Task ListLinksToDedicatedPagesAndKeepsActionsInARowMenu()
    {
        var id = await CreateAsync();
        var cut = Render<WebhooksPage>();
        cut.WaitForAssertion(() => Assert.Contains("Example", cut.Markup));

        Assert.Contains(cut.FindAll("a"), a => a.TextContent == "Example" &&
            a.GetAttribute("href") == $"/Settings/Integrations/Webhooks/{id}/General");
        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Contains("Add webhook") &&
            a.GetAttribute("href") == "/Settings/Integrations/Webhooks/Add");
        Assert.Empty(cut.FindAll("form"));
        Assert.Empty(cut.FindAll("pre"));
        cut.Find("button[aria-label='Actions for Example']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Run test", cut.Markup));
        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Trim() == "Edit" &&
            a.GetAttribute("href") == $"/Settings/Integrations/Webhooks/{id}/General");
        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Trim() == "Deliveries" &&
            a.GetAttribute("href") == $"/Settings/Integrations/Webhooks/{id}/Deliveries");

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Run test").Click();
        cut.WaitForAssertion(() => Assert.Contains($"/{id}/Deliveries?delivery=", Navigation.Uri));
        Assert.Single(await Webhooks.GetDeliveriesAsync(id));
    }

    [Fact]
    public async Task DisabledWebhookCannotBeRunFromTheMenu()
    {
        var id = await CreateAsync(enabled: false);
        var cut = Render<WebhooksPage>();
        cut.WaitForAssertion(() => Assert.Contains("Example", cut.Markup));
        cut.Find("button[aria-label='Actions for Example']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Run test", cut.Markup));
        Assert.True(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Run test").HasAttribute("disabled"));
        Assert.Empty(await Webhooks.GetDeliveriesAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionRequiresConfirmation(bool confirm)
    {
        var id = await CreateAsync();
        var host = Render<ComponentHost>();
        var cut = Render<WebhooksPage>();
        cut.WaitForAssertion(() => Assert.Contains("Example", cut.Markup));
        cut.Find("button[aria-label='Actions for Example']").Click();
        cut.WaitForAssertion(() => Assert.Contains("Delete", cut.Markup));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Delete").Click();
        host.WaitForAssertion(() => Assert.Contains("including its pending deliveries and history", host.Markup));
        host.FindAll(".lc-dialog-footer button").Single(b => b.TextContent.Trim() == (confirm ? "Delete" : "Cancel")).Click();
        cut.WaitForAssertion(() => Assert.Equal(confirm, cut.Markup.Contains("No webhooks registered")));
        Assert.Equal(!confirm, (await Webhooks.GetAsync(id)) != null);
    }

    [Fact]
    public async Task NewEditorPreviewsSelectedEventAndOpensItsTestDelivery()
    {
        var cut = Render<WebhookEditor>();
        using (var preview = JsonDocument.Parse(cut.Find("pre code").TextContent))
            Assert.Equal(ServerEventTypes.GameCreated, preview.RootElement.GetProperty("eventType").GetString());
        await cut.InvokeAsync(() => cut.FindComponent<Select<string>>().Instance.ValueChanged.InvokeAsync(ServerEventTypes.GameInstallFailed));
        using (var preview = JsonDocument.Parse(cut.Find("pre code").TextContent))
        {
            Assert.Equal("failed", preview.RootElement.GetProperty("status").GetString());
            Assert.True(preview.RootElement.GetProperty("payload").GetProperty("reportedByLauncher").GetBoolean());
        }
        await FillAsync(cut, Secret);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save and send test").Click();
        cut.WaitForAssertion(() => Assert.Contains("/Deliveries?delivery=", Navigation.Uri));

        var subscription = Assert.Single(await Webhooks.GetAsync());
        Assert.True(subscription.HasSigningSecret);
        var delivery = Assert.Single(await Webhooks.GetDeliveriesAsync(subscription.Id));
        Assert.Equal(ServerEventTypes.GameInstallFailed, delivery.EventType);
        Assert.Contains($"delivery={delivery.Id}", Navigation.Uri);
        Assert.DoesNotContain("Latest 100 deliveries", cut.Markup);
    }

    [Fact]
    public async Task EventPickerSupportsRemovingAndAddingOnlyKnownEvents()
    {
        var id = await CreateAsync();
        var cut = Render<WebhookEditor>(p => p.Add(x => x.Id, id));
        cut.WaitForAssertion(() => Assert.Contains($"Remove {ServerEventTypes.GameCreated}", cut.Markup));
        var picker = cut.FindComponent<TagsInput<string>>();
        Assert.Null(picker.Instance.OnCreate);
        cut.Find($"button[aria-label='Remove {ServerEventTypes.GameCreated}']").Click();
        picker.Find(".lc-tags-input-search input").Change(ServerEventTypes.ArchiveUploaded);
        picker.Find(".lc-tags-input").KeyDown("Enter");
        cut.WaitForAssertion(() => Assert.Contains($"Remove {ServerEventTypes.ArchiveUploaded}", cut.Markup));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();
        cut.WaitForAssertion(() => Assert.DoesNotContain("Unsaved changes", cut.Markup));
        Assert.Equal([ServerEventTypes.ArchiveUploaded], (await Webhooks.GetAsync(id))!.EventTypes);
    }

    [Fact]
    public async Task NewSubscriptionCanBeSavedWithoutASigningSecret()
    {
        var cut = Render<WebhookEditor>();
        Assert.Contains("Signing secret (optional)", cut.Markup);
        await FillAsync(cut);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();
        cut.WaitForAssertion(() => Assert.Contains("/General", Navigation.Uri));
        Assert.False(Assert.Single(await Webhooks.GetAsync()).HasSigningSecret);
    }

    [Fact]
    public async Task InvalidFormDoesNotSaveOrQueueATest()
    {
        var cut = Render<WebhookEditor>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save and send test").Click();
        Assert.Empty(await Webhooks.GetAsync());
        Assert.DoesNotContain("/Deliveries", Navigation.Uri);
        Assert.Single(cut.FindAll("form"));
    }

    [Fact]
    public async Task ExistingSigningSecretCanBeRemovedFromTheEditor()
    {
        var id = await CreateAsync(secret: Secret);
        var cut = Render<WebhookEditor>(p => p.Add(x => x.Id, id));
        cut.WaitForAssertion(() => Assert.Contains("Remove existing signing secret", cut.Markup));
        await cut.InvokeAsync(() => cut.FindComponent<Checkbox>().Instance.ValueChanged.InvokeAsync(true));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save and send test").Click();
        cut.WaitForAssertion(() => Assert.Contains("/Deliveries?delivery=", Navigation.Uri));
        Assert.False((await Webhooks.GetAsync(id))!.HasSigningSecret);
    }

    [Fact]
    public async Task DeliveriesShowExactJsonAndRefreshTheirResult()
    {
        var id = await CreateAsync();
        var deliveryId = await Webhooks.TestAsync(id);
        Navigation.NavigateTo($"/Settings/Integrations/Webhooks/{id}/Deliveries?delivery={deliveryId}");
        var cut = Render<WebhookDeliveries>(p => p.Add(x => x.Id, id));
        var body = await Webhooks.GetDeliveryBodyAsync(id, deliveryId);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("pre code")));
        AssertFormattedThenRaw();
        Assert.Empty(cut.FindAll("form"));
        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Trim() == "General" &&
            a.GetAttribute("href") == $"/Settings/Integrations/Webhooks/{id}/General");

        await using var context = await Services.GetRequiredService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();
        await context.Set<Server.Data.Models.WebhookDelivery>().Where(d => d.Id == deliveryId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, Server.Data.Models.WebhookDeliveryStatus.Delivered));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Refresh").Click();
        cut.WaitForAssertion(() => Assert.Contains("Delivered", cut.Markup));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("pre code")));
        AssertFormattedThenRaw();

        void AssertFormattedThenRaw()
        {
            var formatted = cut.Find("pre code").TextContent;
            Assert.Contains("\n  \"", formatted);
            Assert.Equal(JsonSerializer.Serialize(JsonDocument.Parse(body!).RootElement), JsonSerializer.Serialize(JsonDocument.Parse(formatted).RootElement));
            Assert.Contains(cut.FindAll("pre code .webhook-json-key"), k => k.TextContent == "\"eventType\"");

            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Show raw").Click();
            Assert.Equal(body, cut.Find("pre code").TextContent);
            cut.FindAll("button").Single(b => b.TextContent.Trim() == "Show formatted").Click();
        }
    }

    [Fact]
    public async Task ChangingSubscriptionDoesNotKeepAnotherWebhooksPayload()
    {
        var firstId = await CreateAsync();
        var secondId = await CreateAsync();
        var deliveryId = await Webhooks.TestAsync(firstId);
        Navigation.NavigateTo($"/Settings/Integrations/Webhooks/{firstId}/Deliveries?delivery={deliveryId}");
        var cut = Render<WebhookDeliveries>(p => p.Add(x => x.Id, firstId));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("pre code")));

        cut.Render(p => p.Add(x => x.Id, secondId));
        cut.WaitForAssertion(() => Assert.Contains("No deliveries yet", cut.Markup));
        Assert.Empty(cut.FindAll("pre"));
        Assert.DoesNotContain(deliveryId.ToString(), cut.Markup);
    }

    [Fact]
    public async Task ChangingEditorSubscriptionReplacesThePreviousForm()
    {
        var id = await CreateAsync();
        var cut = Render<WebhookEditor>(p => p.Add(x => x.Id, id));
        cut.WaitForAssertion(() => Assert.Equal("Example", cut.FindComponent<TextInput>().Instance.Value));
        cut.Render(p => p.Add(x => x.Id, Guid.Empty));
        cut.WaitForAssertion(() => Assert.Equal("", cut.FindComponent<TextInput>().Instance.Value));
        Assert.Contains("New webhook", cut.Markup);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(cut.FindComponent<TagsInput<string>>().Instance.Values));
    }

    [Fact]
    public void MissingWebhooksShowNotFoundInsteadOfAnEditableForm()
    {
        var id = Guid.NewGuid();
        var editor = Render<WebhookEditor>(p => p.Add(x => x.Id, id));
        var deliveries = Render<WebhookDeliveries>(p => p.Add(x => x.Id, id));
        editor.WaitForAssertion(() => Assert.Contains("This webhook no longer exists.", editor.Markup));
        deliveries.WaitForAssertion(() => Assert.Contains("This webhook no longer exists.", deliveries.Markup));
        Assert.Empty(editor.FindAll("form"));
    }

    private static Task FillAsync(IRenderedComponent<WebhookEditor> cut, string? secret = null) =>
        cut.InvokeAsync(async () =>
        {
            var inputs = cut.FindComponents<TextInput>();
            await inputs[0].Instance.ValueChanged.InvokeAsync("Example");
            await inputs[1].Instance.ValueChanged.InvokeAsync("https://example.com/events");
            if (secret != null)
                await cut.FindComponent<PasswordInput>().Instance.ValueChanged.InvokeAsync(secret);
            await cut.FindComponent<TagsInput<string>>().Instance.ValuesChanged.InvokeAsync([ServerEventTypes.GameCreated]);
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
