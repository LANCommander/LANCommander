using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Pages.Servers.Edit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// A plugin's server engine on the server editor: offered on the General page, its editor shown in
/// place of the Local engine's process options and saved with the page, and its own pages and consoles
/// listed under it once the server uses it.
/// </summary>
[Collection("BUnit")]
public class ServerEngineExtensionTests : BUnitTestContext
{
    private const string EngineId = "test.containers";

    private readonly Guid _serverId;
    private readonly PluginServerEngines _engines;

    public ServerEngineExtensionTests(BUnitServerFixture fixture) : base(fixture)
    {
        _engines = new PluginServerEngines([new FakeEngine()], Fixture.Factory.RealServices, NullLogger<PluginServerEngines>.Instance);

        Services.AddSingleton(_engines);

        using var scope = Fixture.Factory.RealServices.CreateScope();

        _serverId = scope.ServiceProvider.GetRequiredService<ServerService>().AddAsync(new ServerModel
        {
            Name = $"Engine Test {Guid.NewGuid():N}",
            Engine = ServerEngine.Local,
            Path = "/srv/game/run.sh",
        }).GetAwaiter().GetResult().Id;
    }

    private IRenderedComponent<General> RenderGeneral()
    {
        var cut = Render<General>(parameters => parameters.Add(p => p.Id, _serverId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".edit-form")), TimeSpan.FromSeconds(10));

        return cut;
    }

    private static bool HasLabel(IRenderedComponent<General> cut, string label) =>
        cut.FindAll("label.lc-form-item-label").Any(l => l.TextContent.Trim() == label);

    private ServerModel Saved()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();

        return scope.ServiceProvider.GetRequiredService<ServerService>().AsNoTracking().GetAsync(_serverId).GetAwaiter().GetResult()!;
    }

    [Fact]
    public async Task ChoosingAPluginEngineShowsItsEditorAndSavesWhatItChanges()
    {
        var cut = RenderGeneral();

        Assert.True(HasLabel(cut, "Executable path"));

        var engine = cut.FindComponents<Select<string>>().First();

        Assert.Contains(engine.Instance.Items!, item => item.Value == $"Extension:{EngineId}" && item.Label == "Containers");

        await cut.InvokeAsync(() => engine.Instance.ValueChanged.InvokeAsync($"Extension:{EngineId}"));

        // The engine's editor, instead of the Local engine's process options
        cut.WaitForAssertion(() => Assert.Contains("fake-engine-editor", cut.Markup));
        Assert.False(HasLabel(cut, "Executable path"));
        Assert.False(HasLabel(cut, "Termination method"));

        await cut.Find(".fake-engine-editor button").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Unsaved changes", cut.Find(".edit-unsaved").TextContent));

        await cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".edit-unsaved")), TimeSpan.FromSeconds(10));

        var saved = Saved();

        Assert.Equal(ServerEngine.Extension, saved.Engine);
        Assert.Equal(EngineId, saved.EngineExtensionId);
        Assert.Equal("""{"container":"arena"}""", saved.EngineSettings);
        Assert.Equal(27015, saved.Port);
    }

    [Fact]
    public async Task AServerUsingAPluginEngineListsItsPages()
    {
        using (var scope = Fixture.Factory.RealServices.CreateScope())
        {
            var servers = scope.ServiceProvider.GetRequiredService<ServerService>();
            var server = (await servers.GetAsync(_serverId))!;

            server.Engine = ServerEngine.Extension;
            server.EngineExtensionId = EngineId;
            server.EngineSettings = """{"container":"arena"}""";

            await servers.UpdateAsync(server);
        }

        var cut = Render<EngineSection>(parameters => parameters
            .Add(p => p.Id, _serverId)
            .Add(p => p.Slug, "Container"));

        cut.WaitForAssertion(() => Assert.Contains("fake-engine-section", cut.Markup), TimeSpan.FromSeconds(10));

        // The page gets the server as its engine sees it
        Assert.Contains("""{"container":"arena"}""", cut.Find(".fake-engine-section").TextContent);
        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == $"/Servers/{_serverId}/Engine/Container");
    }

    [Fact]
    public async Task AServerUsingAPluginEngineListsItsConsolesAndShowsItsLog()
    {
        using (var scope = Fixture.Factory.RealServices.CreateScope())
        {
            var servers = scope.ServiceProvider.GetRequiredService<ServerService>();
            var server = (await servers.GetAsync(_serverId))!;

            server.Engine = ServerEngine.Extension;
            server.EngineExtensionId = EngineId;
            server.EngineSettings = """{"container":"arena"}""";

            await servers.UpdateAsync(server);
        }

        // As the server does once a server has been saved
        foreach (var engine in _engines.GetEngines())
            await engine.RefreshTrackingAsync();

        var cut = Render<LANCommander.Server.UI.Pages.Servers.Edit.Monitor>(parameters => parameters
            .Add(p => p.Id, _serverId)
            .Add(p => p.EngineConsoleId, "logs"));

        cut.WaitForAssertion(() => Assert.Contains("Server ready on arena", cut.Markup), TimeSpan.FromSeconds(10));

        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == $"/Servers/{_serverId}/Monitor/Engine/logs");
        Assert.Contains(cut.FindAll("a"), a => a.GetAttribute("href") == $"/Servers/{_serverId}/Monitor/Engine/shell");
        cut.WaitForAssertion(() => Assert.Contains("The log has ended", cut.Markup));
    }

    [Fact]
    public async Task AServerWhosePluginIsGoneSaysSo()
    {
        using (var scope = Fixture.Factory.RealServices.CreateScope())
        {
            var servers = scope.ServiceProvider.GetRequiredService<ServerService>();
            var server = (await servers.GetAsync(_serverId))!;

            server.Engine = ServerEngine.Extension;
            server.EngineExtensionId = "uninstalled.engine";

            await servers.UpdateAsync(server);
        }

        var cut = RenderGeneral();

        Assert.Contains("uninstalled.engine, isn't installed", cut.Markup);
        Assert.Contains(cut.FindComponents<Select<string>>().First().Instance.Items!, item => item.Label == "uninstalled.engine (not installed)");
    }

    [Fact]
    public void APluginEngineIsConfiguredWithTheBuiltInOnes()
    {
        var cut = Render<LANCommander.Server.UI.Pages.Settings.Components.ServerEngineEditor>(parameters => parameters
            .Add(p => p.Values, [new LANCommander.Server.Settings.Models.ServerEngineConfiguration { Name = "This machine" }]));

        var panel = cut.Find(".server-engine-extension");

        Assert.Equal(EngineId, panel.GetAttribute("data-engine"));
        Assert.Contains("Containers", panel.TextContent);
        Assert.DoesNotContain("No engines have been configured", cut.Markup);

        // Listed after the built-in engines, and opened like them
        Assert.Contains("This machine", cut.Markup);
        Assert.True(cut.Markup.IndexOf("This machine", StringComparison.Ordinal) < cut.Markup.IndexOf("server-engine-extension", StringComparison.Ordinal));

        cut.Find(".server-engine-extension .lc-panel-toggle").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".server-engine-extension .fake-engine-settings")));
    }

    [Fact]
    public async Task APluginEnginesSettingsAreSavedWithThePageAndABrokenOneIsContained()
    {
        // A second engine whose settings throw, both while drawn and while saved
        var engines = new PluginServerEngines([new FakeEngine(), new BrokenEngine()], Fixture.Factory.RealServices, NullLogger<PluginServerEngines>.Instance);

        Services.AddSingleton(engines);

        FakeEngineSettings.Saves = 0;

        var cut = Render<LANCommander.Server.UI.Pages.Settings.Components.ServerEngineEditor>();

        foreach (var toggle in cut.FindAll(".server-engine-extension .lc-panel-toggle").ToList())
            await toggle.ClickAsync(new());

        // The broken engine says so in its own panel; the other still works
        cut.WaitForAssertion(() => Assert.Contains("The Broken engine's settings could not be displayed", cut.Markup));
        Assert.NotNull(cut.Find(".fake-engine-settings"));

        var failures = await cut.InvokeAsync(() => cut.Instance.SaveExtensionsAsync());

        Assert.Equal(1, FakeEngineSettings.Saves);
        Assert.Empty(failures);

        // Closing the panel disposes the component, which stops it being saved
        await cut.Find($".server-engine-extension[data-engine=\"{EngineId}\"] .lc-panel-toggle").ClickAsync(new());
        await cut.InvokeAsync(() => cut.Instance.SaveExtensionsAsync());

        Assert.Equal(1, FakeEngineSettings.Saves);
    }

    // The menu is a lazy popup, drawn once it's first opened
    private static async Task OpenAddMenuAsync(IRenderedComponent<LANCommander.Server.UI.Pages.Settings.Components.ServerEngineEditor> cut)
    {
        if (cut.FindAll(".lc-menu-item").Count == 0)
            await cut.Find(".server-engine-add").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".lc-menu-item")));
    }

    [Fact]
    public async Task TheAddMenuListsTheBuiltInAndPluginEngines()
    {
        IEnumerable<LANCommander.Server.Settings.Models.ServerEngineConfiguration>? values = null;

        var cut = Render<LANCommander.Server.UI.Pages.Settings.Components.ServerEngineEditor>(parameters => parameters
            .Add(p => p.ValuesChanged, v => values = v));

        Assert.DoesNotContain(cut.FindAll("button"), b => b.TextContent.Trim() is "Add Local" or "Add Remote");

        await OpenAddMenuAsync(cut);

        var items = cut.FindComponents<MenuItem>();

        Assert.Equal(["Local", "Remote", "Containers"], items.Select(i => i.Find(".lc-menu-item-text").TextContent.Trim()));

        // A built-in engine is added, and opened
        await items.First().Find("button").ClickAsync(new());

        var added = Assert.Single(values!);

        Assert.Equal(ServerEngine.Local, added.Type);
    }

    [Fact]
    public async Task ChoosingAPluginEngineFromTheAddMenuOpensItsPanelAndAsksItToAdd()
    {
        FakeEngineSettings.Adds = 0;

        var cut = Render<LANCommander.Server.UI.Pages.Settings.Components.ServerEngineEditor>();

        Assert.Empty(cut.FindAll(".fake-engine-settings"));

        // Chosen while its panel is closed: it's opened, and the add waits for its component
        await OpenAddMenuAsync(cut);
        await cut.Find($".lc-menu-item[data-engine=\"{EngineId}\"] button").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Contains("Adding", cut.Find(".fake-engine-settings").TextContent));
        Assert.Equal(1, FakeEngineSettings.Adds);

        // Chosen again with it open, it's asked straight away
        await OpenAddMenuAsync(cut);
        await cut.Find($".lc-menu-item[data-engine=\"{EngineId}\"] button").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal(2, FakeEngineSettings.Adds));
    }

    [Fact]
    public async Task AFailedSaveIsReportedByEngine()
    {
        var engines = new PluginServerEngines([new FailingSaveEngine()], Fixture.Factory.RealServices, NullLogger<PluginServerEngines>.Instance);

        Services.AddSingleton(engines);

        var cut = Render<LANCommander.Server.UI.Pages.Settings.Components.ServerEngineEditor>();

        await cut.Find(".server-engine-extension .lc-panel-toggle").ClickAsync(new());

        var failures = await cut.InvokeAsync(() => cut.Instance.SaveExtensionsAsync());

        var failure = Assert.Single(failures);

        Assert.Equal("Failing", failure.Engine);
        Assert.Equal("host unreachable", failure.Error.Message);
    }

    private sealed class BrokenEngine : MinimalEngine
    {
        public override string Id => "test.broken";
        public override string Name => "Broken";
        public override Type? SettingsComponent => typeof(BrokenEngineSettings);
    }

    private sealed class FailingSaveEngine : MinimalEngine
    {
        public override string Id => "test.failing";
        public override string Name => "Failing";
        public override Type? SettingsComponent => typeof(FailingSaveSettings);
    }

    private abstract class MinimalEngine : IServerEngineExtension
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        public abstract Type? SettingsComponent { get; }

        public Task StartAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ServerEngineStatus> GetStatusAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.FromResult(ServerEngineStatus.Stopped);
    }

    public sealed class BrokenEngineSettings : ComponentBase
    {
        [Parameter] public ServerEngineSettingsContext Context { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder) => throw new InvalidOperationException("plugin bug");
    }

    public sealed class FailingSaveSettings : ComponentBase, IDisposable
    {
        [Parameter] public ServerEngineSettingsContext Context { get; set; } = default!;

        IDisposable? _registration;

        protected override void OnInitialized() =>
            _registration = Context.OnSave(_ => throw new InvalidOperationException("host unreachable"));

        public void Dispose() => _registration?.Dispose();
    }

    private sealed class FakeEngine : IServerEngineExtension
    {
        public string Id => EngineId;
        public string Name => "Containers";
        public ServerEngineFeatures Features => ServerEngineFeatures.Consoles;
        public Type? EditorComponent => typeof(FakeEditor);
        public Type? SettingsComponent => typeof(FakeEngineSettings);
        public IReadOnlyList<ServerEngineSection> Sections { get; } = [new("Container", "Container", typeof(FakeSection))];

        public Task StartAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ServerEngineStatus> GetStatusAsync(ServerEngineServer server, CancellationToken cancellationToken) => Task.FromResult(ServerEngineStatus.Stopped);

        public IReadOnlyList<ServerEngineConsole> GetConsoles(ServerEngineServer server) =>
        [
            new("logs", "Container log", ServerEngineConsoleKind.Log),
            new("shell", "Shell", ServerEngineConsoleKind.Terminal),
        ];

        public async IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ServerEngineServer server, string consoleId, ServerEngineLogRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();

            yield return new ServerEngineLogLine($"Server ready on {server.GetSettings<FakeSettings>().Container}", DateTime.UtcNow);
        }
    }

    public sealed class FakeSettings
    {
        public string Container { get; set; } = "";
    }

    /// <summary>Chooses a container, and the port it publishes.</summary>
    public sealed class FakeEditor : ComponentBase
    {
        [Parameter] public ServerEngineEditorContext Context { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "fake-engine-editor");
            builder.OpenElement(2, "button");
            builder.AddAttribute(3, "type", "button");
            builder.AddAttribute(4, "onclick", EventCallback.Factory.Create(this, () =>
            {
                Context.SetSettings(new FakeSettings { Container = "arena" });
                Context.SetAddress(null, 27015);
            }));
            builder.AddContent(5, "Use arena");
            builder.CloseElement();
            builder.CloseElement();
        }
    }

    public sealed class FakeEngineSettings : ComponentBase, IDisposable
    {
        public static int Saves;
        public static int Adds;

        [Parameter] public ServerEngineSettingsContext Context { get; set; } = default!;

        IDisposable? _registration;
        IDisposable? _addRegistration;
        bool _adding;

        protected override void OnInitialized()
        {
            _registration = Context.OnSave(_ =>
            {
                Interlocked.Increment(ref Saves);

                return Task.CompletedTask;
            });

            _addRegistration = Context.OnAdd(() =>
            {
                Interlocked.Increment(ref Adds);
                _adding = true;

                return Task.CompletedTask;
            });
        }

        public void Dispose()
        {
            _registration?.Dispose();
            _addRegistration?.Dispose();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "fake-engine-settings");
            builder.AddContent(2, _adding ? "Adding a host" : "Hosts");
            builder.CloseElement();
        }
    }

    public sealed class FakeSection : ComponentBase
    {
        [Parameter] public ServerEngineServer Server { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "fake-engine-section");
            builder.AddContent(2, Server.Settings);
            builder.CloseElement();
        }
    }
}
