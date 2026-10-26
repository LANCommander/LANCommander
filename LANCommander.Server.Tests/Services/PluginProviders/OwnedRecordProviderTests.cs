using LANCommander.SDK.Enums;
using LANCommander.Server.Plugins;
using Shouldly;

namespace LANCommander.Server.Tests.Services.PluginProviders;

[Collection("Application")]
public class OwnedRecordProviderTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    async Task<GameInfo> AddGameAsync() =>
        await GetService<IGameProvider>().AddAsync(new GameInfo { Title = $"Owned records {Guid.NewGuid():N}" });

    [Fact]
    public async Task AGamesActionsBelongToItsLatestVersion()
    {
        var game = await AddGameAsync();
        var actions = GetService<IActionProvider>();
        var versions = GetService<IGameVersionProvider>();

        var play = await actions.AddAsync(RecordOwner.Game(game.Id), new ActionInfo { Name = "Play", Path = "game.exe", IsPrimary = true });
        var latest = (await versions.GetLatestAsync(game.Id))!;

        play.Owner.ShouldBe(RecordOwner.Game(game.Id, latest.Id));
        (await actions.GetAsync(RecordOwner.Game(game.Id))).ShouldHaveSingleItem().Id.ShouldBe(play.Id);

        // A new version carries the action over as its own copy; the old version keeps the original
        var next = await versions.CreateAsync(game.Id, "2.0");

        (await actions.GetAsync(RecordOwner.Game(game.Id))).ShouldHaveSingleItem().Owner.VersionId.ShouldBe(next.Id);
        (await actions.GetAsync(RecordOwner.Game(game.Id, latest.Id))).ShouldHaveSingleItem().Id.ShouldBe(play.Id);

        var updated = await actions.UpdateAsync(play with { Arguments = "-windowed" });

        updated.Arguments.ShouldBe("-windowed");
        updated.Owner.ShouldBe(play.Owner);

        await actions.DeleteAsync(play.Id);

        (await actions.GetRecordAsync(play.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ToolsAndServersHaveActionsToo()
    {
        var tool = await GetService<IToolProvider>().AddAsync(new ToolInfo { Name = $"Tool {Guid.NewGuid():N}" });
        var server = await GetService<IServerProvider>().AddAsync(new ServerInfo { Name = $"Server {Guid.NewGuid():N}" });
        var actions = GetService<IActionProvider>();

        await actions.AddAsync(RecordOwner.Tool(tool.Id), new ActionInfo { Name = "Run tool" });
        await actions.AddAsync(RecordOwner.Server(server.Id), new ActionInfo { Name = "Connect" });

        (await actions.GetAsync(RecordOwner.Tool(tool.Id))).ShouldHaveSingleItem().Name.ShouldBe("Run tool");
        (await actions.GetAsync(RecordOwner.Server(server.Id))).ShouldHaveSingleItem().Name.ShouldBe("Connect");
    }

    [Fact]
    public async Task AnOwnerOfTheWrongKindOrThatIsMissingIsRefused()
    {
        var redistributable = await GetService<IRedistributableProvider>().AddAsync(new RedistributableInfo { Name = $"Runtime {Guid.NewGuid():N}" });

        await Should.ThrowAsync<ArgumentException>(() =>
            GetService<IActionProvider>().AddAsync(RecordOwner.Redistributable(redistributable.Id), new ActionInfo { Name = "Nope" }));

        await Should.ThrowAsync<KeyNotFoundException>(() =>
            GetService<IActionProvider>().AddAsync(RecordOwner.Game(Guid.NewGuid()), new ActionInfo { Name = "Nope" }));

        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await GetService<ISavePathProvider>().GetAsync(RecordOwner.Game((await AddGameAsync()).Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task ScriptsAreManagedButPackageScriptsAreNot()
    {
        var game = await AddGameAsync();
        var redistributable = await GetService<IRedistributableProvider>().AddAsync(new RedistributableInfo { Name = $"Runtime {Guid.NewGuid():N}" });
        var scripts = GetService<IScriptProvider>();

        var install = await scripts.AddAsync(RecordOwner.Game(game.Id), new ScriptInfo { Name = "Install", Type = ScriptType.Install, Contents = "Write-Host 1" });
        await scripts.AddAsync(RecordOwner.Redistributable(redistributable.Id), new ScriptInfo { Name = "Detect", Type = ScriptType.DetectInstall, Contents = "$true" });

        (await scripts.GetAsync(RecordOwner.Game(game.Id))).ShouldHaveSingleItem().Id.ShouldBe(install.Id);
        (await scripts.GetAsync(RecordOwner.Redistributable(redistributable.Id))).ShouldHaveSingleItem().Type.ShouldBe(ScriptType.DetectInstall);

        (await scripts.UpdateAsync(install with { Contents = "Write-Host 2" })).Contents.ShouldBe("Write-Host 2");

        await Should.ThrowAsync<ArgumentException>(() =>
            scripts.AddAsync(RecordOwner.Game(game.Id), new ScriptInfo { Name = "Package", Type = ScriptType.Package }));
    }

    [Fact]
    public async Task SavePathsAndMultiplayerModesBelongToGames()
    {
        var game = await AddGameAsync();
        var savePaths = GetService<ISavePathProvider>();
        var modes = GetService<IMultiplayerModeProvider>();

        var saves = await savePaths.AddAsync(RecordOwner.Game(game.Id), new SavePathInfo { Type = SavePathType.File, Path = "{InstallDir}\\Saves" });
        var lan = await modes.AddAsync(RecordOwner.Game(game.Id), new MultiplayerModeInfo { Type = MultiplayerType.LAN, MinPlayers = 2, MaxPlayers = 8 });

        (await savePaths.GetAsync(RecordOwner.Game(game.Id))).ShouldHaveSingleItem().Path.ShouldBe(saves.Path);
        (await modes.UpdateAsync(lan with { MaxPlayers = 16 })).MaxPlayers.ShouldBe(16);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await modes.GetAsync(RecordOwner.Tool(Guid.NewGuid())));
    }

    [Fact]
    public async Task KeysAreAddedOnceAndCanBeReleased()
    {
        var game = await AddGameAsync();
        var keys = GetService<IKeyProvider>();

        var added = await keys.AddRangeAsync(game.Id, ["AAAA-1111", " AAAA-1111 ", "", "BBBB-2222"]);

        added.Select(key => key.Value).ShouldBe(["AAAA-1111", "BBBB-2222"]);
        (await keys.AddRangeAsync(game.Id, ["BBBB-2222"])).ShouldBeEmpty();
        (await keys.GetAsync(RecordOwner.Game(game.Id))).Count.ShouldBe(2);

        var released = await keys.ReleaseAsync(added[0].Id);

        released.AllocationMethod.ShouldBeNull();
        released.ClaimedOn.ShouldBeNull();
    }
}
