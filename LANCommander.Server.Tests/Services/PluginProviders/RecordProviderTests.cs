using LANCommander.SDK.Enums;
using LANCommander.Server.Plugins;
using Shouldly;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LANCommander.Server.Tests.Services.PluginProviders;

/// <summary>Redistributables, tools, servers, game versions and media.</summary>
[Collection("Application")]
public class RecordProviderTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public async Task RedistributablesAreAddedFoundUpdatedAndDeleted()
    {
        var redistributables = GetService<IRedistributableProvider>();
        var name = $"Runtime {Guid.NewGuid():N}";

        var added = await redistributables.AddAsync(new RedistributableInfo { Name = name, Description = "A runtime" });

        (await redistributables.FindAsync(name.ToUpperInvariant()))!.Id.ShouldBe(added.Id);
        (await redistributables.GetAsync()).Select(r => r.Name).ShouldBe(
            (await redistributables.GetAsync()).Select(r => r.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));

        (await redistributables.UpdateAsync(added with { Notes = "Noted" })).Notes.ShouldBe("Noted");

        await redistributables.DeleteAsync(added.Id);

        (await redistributables.GetAsync(added.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ToolsAreAddedUpdatedAndDeleted()
    {
        var tools = GetService<IToolProvider>();

        var added = await tools.AddAsync(new ToolInfo { Name = $"Tool {Guid.NewGuid():N}", AlwaysInstall = true });

        (await tools.UpdateAsync(added with { AlwaysInstall = false })).AlwaysInstall.ShouldBeFalse();

        await tools.DeleteAsync(added.Id);

        (await tools.GetAsync(added.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task ServersHaveConsolesAndHttpPaths()
    {
        var servers = GetService<IServerProvider>();
        var game = await GetService<IGameProvider>().AddAsync(new GameInfo { Title = $"Served {Guid.NewGuid():N}" });

        var server = await servers.AddAsync(new ServerInfo { Name = $"Server {Guid.NewGuid():N}", GameId = game.Id, Engine = ServerHostEngine.Extension, EngineExtensionId = "test.engine", EngineSettings = "{\"hostId\":null}", Port = 27015 });

        server.Engine.ShouldBe(ServerHostEngine.Extension);
        server.EngineExtensionId.ShouldBe("test.engine");
        server.EngineSettings.ShouldBe("{\"hostId\":null}");
        server.GameId.ShouldBe(game.Id);

        (await servers.UpdateAsync(server with { Port = 27016, Autostart = true })).Port.ShouldBe(27016);

        var console = await servers.AddConsoleAsync(server.Id, new ServerConsoleInfo { Name = "Log", Type = ServerConsoleType.LogFile, Path = "server.log" });
        var path = await servers.AddHttpPathAsync(server.Id, new ServerHttpPathInfo { LocalPath = "maps", Path = "/maps" });

        (await servers.UpdateConsoleAsync(console with { Path = "logs/server.log" })).Path.ShouldBe("logs/server.log");
        (await servers.GetConsolesAsync(server.Id)).ShouldHaveSingleItem().ServerId.ShouldBe(server.Id);
        (await servers.GetHttpPathsAsync(server.Id)).ShouldHaveSingleItem().Id.ShouldBe(path.Id);

        await servers.DeleteHttpPathAsync(path.Id);
        await servers.DeleteConsoleAsync(console.Id);

        (await servers.GetHttpPathsAsync(server.Id)).ShouldBeEmpty();

        await servers.DeleteAsync(server.Id);

        (await servers.GetAsync(server.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task GameVersionsAreCreatedUpdatedAndDeletedWithWhatTheyOwn()
    {
        var game = await GetService<IGameProvider>().AddAsync(new GameInfo { Title = $"Versioned {Guid.NewGuid():N}" });
        var versions = GetService<IGameVersionProvider>();
        var scripts = GetService<IScriptProvider>();

        var first = await versions.CreateAsync(game.Id, "1.0", "First");
        var second = await versions.CreateAsync(game.Id, "1.1");

        (await versions.GetAsync(game.Id)).Select(v => v.Id).ShouldBe([first.Id, second.Id]);
        (await versions.GetLatestAsync(game.Id))!.Id.ShouldBe(second.Id);

        var updated = await versions.UpdateAsync(second with { Changelog = "Fixes", Published = true });

        updated.Changelog.ShouldBe("Fixes");
        updated.Published.ShouldBeTrue();

        var script = await scripts.AddAsync(RecordOwner.Game(game.Id, second.Id), new ScriptInfo { Name = "Install", Type = ScriptType.Install });
        var redistributable = await GetService<IRedistributableProvider>().AddAsync(new RedistributableInfo { Name = $"Runtime {Guid.NewGuid():N}" });

        // Linked on the latest version only, so the game's list follows it out
        await GetService<IGameProvider>().SetLinksAsync(game.Id, GameLinkKind.Redistributables, [redistributable.Id]);

        await versions.DeleteAsync(second.Id);

        (await versions.GetVersionAsync(second.Id)).ShouldBeNull();
        (await scripts.GetRecordAsync(script.Id)).ShouldBeNull();
        (await versions.GetLatestAsync(game.Id))!.Id.ShouldBe(first.Id);
        (await GetService<IGameProvider>().GetLinksAsync(game.Id, GameLinkKind.Redistributables)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MediaIsStoredReadReplacedAndDeleted()
    {
        await EnsureStorageLocationsExistAsync();

        var game = await GetService<IGameProvider>().AddAsync(new GameInfo { Title = $"Pictured {Guid.NewGuid():N}" });
        var media = GetService<IMediaProvider>();

        var cover = await media.AddAsync(game.Id, new MediaInfo { Type = MediaType.Cover, MimeType = "image/png" }, Png(64, 96));

        cover.GameId.ShouldBe(game.Id);
        cover.Crc32.ShouldNotBeNullOrEmpty();
        (await media.GetAsync(game.Id)).ShouldHaveSingleItem().Id.ShouldBe(cover.Id);

        var replaced = await media.ReplaceFileAsync(cover.Id, Png(32, 48));

        replaced.Crc32.ShouldNotBe(cover.Crc32);

        await using (var file = await media.OpenReadAsync(cover.Id))
            file.ShouldNotBeNull().Length.ShouldBeGreaterThan(0);

        (await media.UpdateAsync(replaced with { Name = "Box art", SortOrder = 2 })).Name.ShouldBe("Box art");

        await media.DeleteAsync(cover.Id);

        (await media.GetMediaAsync(cover.Id)).ShouldBeNull();
        (await media.OpenReadAsync(cover.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeletingAGameDeletesItsMediaFiles()
    {
        await EnsureStorageLocationsExistAsync();

        var games = GetService<IGameProvider>();
        var game = await games.AddAsync(new GameInfo { Title = $"Deleted {Guid.NewGuid():N}" });
        var cover = await GetService<IMediaProvider>().AddAsync(game.Id, new MediaInfo { Type = MediaType.Cover, MimeType = "image/png" }, Png(16, 16));

        var mediaService = GetService<LANCommander.Server.Services.MediaService>();
        var path = LANCommander.Server.Services.MediaService.GetMediaPath(
            (await mediaService.Include(m => m.StorageLocation).GetAsync(cover.Id))!);

        File.Exists(path).ShouldBeTrue();

        await games.DeleteAsync(game.Id);

        File.Exists(path).ShouldBeFalse();
        (await GetService<IMediaProvider>().GetMediaAsync(cover.Id)).ShouldBeNull();
    }

    static MemoryStream Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(40, 90, 160));

        var stream = new MemoryStream();

        image.SaveAsPng(stream);
        stream.Position = 0;

        return stream;
    }
}
