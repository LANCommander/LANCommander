using System.Collections.Concurrent;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using DataServerConsole = LANCommander.Server.Data.Models.ServerConsole;
using ServerModel = LANCommander.Server.Data.Models.Server;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// The events the server raises for plugins through <see cref="IPluginEventBus"/>, observed through
/// the application's own bus the way a plugin subscribes to it.
/// </summary>
[Collection("Application")]
public class PluginServerEventTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private sealed class Recorder<TEvent> : IDisposable
    {
        private readonly IDisposable _subscription;

        public ConcurrentQueue<TEvent> Events { get; } = new();

        public Recorder(IPluginEventBus bus) =>
            _subscription = bus.Subscribe<TEvent>((e, _) =>
            {
                Events.Enqueue(e);
                return Task.CompletedTask;
            });

        public void Dispose() => _subscription.Dispose();
    }

    private Recorder<TEvent> Record<TEvent>() => new(GetService<IPluginEventBus>());

    [Fact]
    public async Task GamesRaiseCreatedUpdatedAndDeleted()
    {
        using var created = Record<GameCreatedEvent>();
        using var updated = Record<GameUpdatedEvent>();
        using var deleted = Record<GameDeletedEvent>();

        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        game.Title = Unique("Renamed");
        await gameService.UpdateAsync(game);

        await gameService.DeleteAsync(game);

        created.Events.ShouldContain(e => e.GameId == game.Id);
        updated.Events.ShouldContain(e => e.GameId == game.Id && e.Title == game.Title);
        deleted.Events.ShouldContain(e => e.GameId == game.Id && e.Title == game.Title);
    }

    [Fact]
    public async Task AFinishedUploadRaisesArchiveUploaded()
    {
        using var uploaded = Record<ArchiveUploadedEvent>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);

        uploaded.Events.ShouldNotContain(e => e.ArchiveId == archive.Id);

        await GetService<ArchiveService>().PublishUploadedAsync(archive.Id);

        var @event = uploaded.Events.Where(e => e.ArchiveId == archive.Id).ShouldHaveSingleItem();
        @event.ArchiveId.ShouldBe(archive.Id);
        @event.GameId.ShouldBe(game.Id);
        @event.RedistributableId.ShouldBeNull();
        @event.Version.ShouldBe(archive.Version);
    }

    [Fact]
    public async Task UsersRaiseCreatedApprovedOnlyWhenApprovalChangesAndDeleted()
    {
        using var created = Record<UserCreatedEvent>();
        using var approved = Record<UserApprovedEvent>();
        using var deleted = Record<UserDeletedEvent>();

        var userService = GetService<UserService>();

        var user = await userService.AddAsync(new User { UserName = Unique("user"), Approved = false });

        created.Events.ShouldContain(e => e.UserId == user.Id && !e.Approved);

        // Saving an unapproved user, then approving, then saving again approved
        await userService.UpdateAsync(user);

        user.Approved = true;
        user.ApprovedOn = DateTime.UtcNow;
        await userService.UpdateAsync(user);
        await userService.UpdateAsync(user);

        approved.Events.Where(e => e.UserId == user.Id).ShouldHaveSingleItem();

        await userService.DeleteAsync(user);

        deleted.Events.ShouldContain(e => e.UserId == user.Id && e.UserName == user.UserName);
    }

    [Fact]
    public async Task PlaySessionsRaiseStartedAndEnded()
    {
        using var started = Record<PlaySessionStartedEvent>();
        using var ended = Record<PlaySessionEndedEvent>();

        var user = await EnsureAdminUserCreatedAsync();
        var game = await AddGameAsync();
        var playSessions = GetService<PlaySessionService>();

        await playSessions.StartSessionAsync(game.Id, user.Id);
        await playSessions.EndSessionAsync(game.Id, user.Id);

        var start = started.Events.Single(e => e.GameId == game.Id);
        var end = ended.Events.Single(e => e.GameId == game.Id);

        end.SessionId.ShouldBe(start.SessionId);
        end.UserId.ShouldBe(user.Id);
        end.TimedOut.ShouldBeFalse();
        end.End.ShouldBeGreaterThanOrEqualTo(end.Start);
    }

    [Fact]
    public async Task EndingAGameWithNoOpenSessionRaisesNothing()
    {
        using var ended = Record<PlaySessionEndedEvent>();

        var user = await EnsureAdminUserCreatedAsync();
        var game = await AddGameAsync();

        await GetService<PlaySessionService>().EndSessionAsync(game.Id, user.Id);

        ended.Events.ShouldNotContain(e => e.GameId == game.Id);
    }

    [Fact]
    public async Task IssuesRaiseCreatedAndResolved()
    {
        using var created = Record<IssueCreatedEvent>();
        using var resolved = Record<IssueResolvedEvent>();

        var user = await EnsureAdminUserCreatedAsync();
        var game = await AddGameAsync();
        var issueService = GetService<IssueService>();

        var issue = await issueService.AddAsync(new Issue { GameId = game.Id, Description = "It crashes" });
        await issueService.ResolveAsync(issue.Id, user.Id);

        created.Events.ShouldContain(e => e.IssueId == issue.Id && e.GameId == game.Id && e.Description == "It crashes");
        resolved.Events.ShouldContain(e => e.IssueId == issue.Id && e.ResolvedByUserId == user.Id);
    }

    [Fact]
    public async Task KeysRaiseAllocatedAndReleasedWithTheRemainingCount()
    {
        using var allocated = Record<KeyAllocatedEvent>();
        using var released = Record<KeyReleasedEvent>();

        var user = await EnsureAdminUserCreatedAsync();
        var game = await AddGameAsync();
        var first = await AddKeyAsync(game.Id);
        var second = await AddKeyAsync(game.Id);
        var keyService = GetService<KeyService>();

        await keyService.AllocateAsync(first, user);
        await keyService.AllocateAsync(second, "00-11-22-33-44-55");

        var byUser = allocated.Events.Single(e => e.KeyId == first.Id);
        byUser.GameId.ShouldBe(game.Id);
        byUser.Method.ShouldBe(LANCommander.SDK.Enums.KeyAllocationMethod.UserAccount);
        byUser.UserId.ShouldBe(user.Id);
        byUser.MacAddress.ShouldBeNull();
        byUser.AvailableKeys.ShouldBe(1);

        var byMac = allocated.Events.Single(e => e.KeyId == second.Id);
        byMac.Method.ShouldBe(LANCommander.SDK.Enums.KeyAllocationMethod.MacAddress);
        byMac.UserId.ShouldBeNull();
        byMac.MacAddress.ShouldBe("00-11-22-33-44-55");
        byMac.AvailableKeys.ShouldBe(0);

        await keyService.ReleaseAsync(first.Id);
        await keyService.ReleaseAsync(second);

        var fromUser = released.Events.Single(e => e.KeyId == first.Id);
        fromUser.UserId.ShouldBe(user.Id);
        fromUser.MacAddress.ShouldBeNull();
        fromUser.AvailableKeys.ShouldBe(1);

        var fromMac = released.Events.Single(e => e.KeyId == second.Id);
        fromMac.UserId.ShouldBeNull();
        fromMac.MacAddress.ShouldBe("00-11-22-33-44-55");
        fromMac.AvailableKeys.ShouldBe(2);

        // Releasing a key nobody holds isn't a release.
        await keyService.ReleaseAsync(first.Id);

        released.Events.Count(e => e.KeyId == first.Id).ShouldBe(1);
    }

    [Fact]
    public async Task DeletingASaveRaisesSaveDeleted()
    {
        using var deleted = Record<SaveDeletedEvent>();

        var user = await EnsureAdminUserCreatedAsync();
        var game = await AddGameAsync();
        var saveService = GetService<GameSaveService>();

        var save = await saveService.AddAsync(new GameSave
        {
            GameId = game.Id,
            UserId = user.Id,
            StorageLocationId = (await GetStorageLocationAsync(LANCommander.SDK.Enums.StorageLocationType.Save)).Id,
        });

        await saveService.DeleteAsync(save);

        var @event = deleted.Events.Where(e => e.SaveId == save.Id).ShouldHaveSingleItem();
        @event.GameId.ShouldBe(game.Id);
        @event.UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task GameVersionsRaiseCreatedUpdatedPublishedAndDeleted()
    {
        using var created = Record<GameVersionCreatedEvent>();
        using var updated = Record<GameVersionUpdatedEvent>();
        using var published = Record<GameVersionPublishedChangedEvent>();
        using var deleted = Record<GameVersionDeletedEvent>();

        var game = await AddGameAsync();
        var versions = GetService<GameVersionService>();

        var first = await versions.CreateAsync(game.Id, "1.0");
        await versions.SetPublishedAsync(first.Id, false);

        // Adding a version after a draft publishes the draft
        var second = await versions.CreateAsync(game.Id, "1.1", "Fixes");

        created.Events.ShouldContain(e => e.VersionId == first.Id && e.GameId == game.Id && e.Version == "1.0");
        created.Events.ShouldContain(e => e.VersionId == second.Id && e.Version == "1.1" && !e.Published);
        published.Events.Where(e => e.VersionId == first.Id).Select(e => e.Published).ShouldBe([false, true]);

        // No change, no event
        await versions.SetPublishedAsync(first.Id, true);
        published.Events.Count(e => e.VersionId == first.Id).ShouldBe(2);

        var loaded = await versions.GetAsync(second.Id);
        loaded.Changelog = "More fixes";
        await versions.UpdateAsync(loaded);

        updated.Events.ShouldContain(e => e.VersionId == second.Id && e.Version == "1.1");

        await versions.DeleteAsync(await versions.GetAsync(second.Id));

        deleted.Events.ShouldContain(e => e.VersionId == second.Id && e.GameId == game.Id && e.Version == "1.1");
    }

    [Fact]
    public async Task RedistributablesRaiseCreatedUpdatedAndDeleted()
    {
        using var created = Record<RedistributableCreatedEvent>();
        using var updated = Record<RedistributableUpdatedEvent>();
        using var deleted = Record<RedistributableDeletedEvent>();

        var service = GetService<RedistributableService>();
        var redistributable = await AddRedistributableAsync();

        redistributable.Name = Unique("Renamed");
        await service.UpdateAsync(redistributable);

        // Plugins delete by id alone; the name still comes through
        await service.DeleteAsync(new Redistributable { Id = redistributable.Id });

        created.Events.ShouldContain(e => e.RedistributableId == redistributable.Id);
        updated.Events.ShouldContain(e => e.RedistributableId == redistributable.Id && e.Name == redistributable.Name);
        deleted.Events.ShouldContain(e => e.RedistributableId == redistributable.Id && e.Name == redistributable.Name);
    }

    [Fact]
    public async Task ToolsRaiseCreatedUpdatedAndDeleted()
    {
        using var created = Record<ToolCreatedEvent>();
        using var updated = Record<ToolUpdatedEvent>();
        using var deleted = Record<ToolDeletedEvent>();

        var service = GetService<ToolService>();
        var tool = await service.AddAsync(new Tool { Name = Unique("Tool") });

        tool.Name = Unique("Renamed");
        await service.UpdateAsync(tool);

        await service.DeleteAsync(new Tool { Id = tool.Id });

        created.Events.ShouldContain(e => e.ToolId == tool.Id);
        updated.Events.ShouldContain(e => e.ToolId == tool.Id && e.Name == tool.Name);
        deleted.Events.ShouldContain(e => e.ToolId == tool.Id && e.Name == tool.Name);
    }

    [Fact]
    public async Task ServersRaiseCreatedUpdatedAndDeleted()
    {
        using var created = Record<ServerCreatedEvent>();
        using var updated = Record<ServerUpdatedEvent>();
        using var deleted = Record<ServerDeletedEvent>();

        var service = GetService<ServerService>();
        var server = await AddServerAsync();

        server.Name = Unique("Renamed");
        await service.UpdateAsync(server);

        await service.DeleteAsync(server);

        created.Events.ShouldContain(e => e.ServerId == server.Id);
        updated.Events.ShouldContain(e => e.ServerId == server.Id && e.Name == server.Name);
        deleted.Events.ShouldContain(e => e.ServerId == server.Id && e.Name == server.Name);
    }

    [Fact]
    public async Task MediaRaisesCreatedUpdatedAndDeleted()
    {
        using var created = Record<MediaCreatedEvent>();
        using var updated = Record<MediaUpdatedEvent>();
        using var deleted = Record<MediaDeletedEvent>();

        var service = GetService<MediaService>();
        var game = await AddGameAsync();
        var first = await AddMediaAsync(game.Id);
        var second = await AddMediaAsync(game.Id);

        first.Type = LANCommander.SDK.Enums.MediaType.Icon;
        await service.UpdateAsync(first);

        // Deleting removes the file too, which needs to know where it is stored
        first.StorageLocation = second.StorageLocation =
            await GetStorageLocationAsync(LANCommander.SDK.Enums.StorageLocationType.Media);

        await service.DeleteAsync(first);
        await service.DeleteRangeAsync([second]);

        created.Events.ShouldContain(e => e.MediaId == first.Id && e.GameId == game.Id);
        updated.Events.ShouldContain(e => e.MediaId == first.Id && e.Type == LANCommander.SDK.Enums.MediaType.Icon);
        deleted.Events.ShouldContain(e => e.MediaId == first.Id && e.GameId == game.Id);
        deleted.Events.ShouldContain(e => e.MediaId == second.Id && e.GameId == game.Id);
    }

    [Fact]
    public async Task LibrariesRaiseGameAddedAndRemoved()
    {
        using var added = Record<LibraryGameAddedEvent>();
        using var removed = Record<LibraryGameRemovedEvent>();

        var user = await GetService<UserService>().AddAsync(new User { UserName = Unique("player"), Approved = true });
        var first = await AddGameAsync();
        var second = await AddGameAsync();
        var library = GetService<LibraryService>();

        await library.AddToLibraryAsync(user.Id, first.Id);
        await library.AddToLibraryAsync(user.Id, second.Id);
        await library.RemoveFromLibraryAsync(user.Id, first.Id);

        added.Events.Where(e => e.UserId == user.Id).Select(e => e.GameId).ShouldBe([first.Id, second.Id]);
        removed.Events.Where(e => e.UserId == user.Id).Select(e => e.GameId).ShouldBe([first.Id]);
    }

    private sealed class StatusEngine : IServerEngine
    {
        public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
        public event EventHandler<ServerLogEventArgs>? OnServerLog;

        public Task InitializeAsync() => Task.CompletedTask;
        public Task RefreshTrackingAsync() => Task.CompletedTask;
        public bool IsManaging(Guid serverId) => false;
        public Task StartAsync(Guid serverId) => Task.CompletedTask;
        public Task StopAsync(Guid serverId) => Task.CompletedTask;
        public Task<ServerProcessStatus> GetStatusAsync(Guid serverId) => Task.FromResult(ServerProcessStatus.Stopped);

        public void Raise(ServerStatusUpdateEventArgs args) => OnServerStatusUpdate?.Invoke(this, args);
    }

    private sealed class NoRcon : IRconCommandSender
    {
        public bool CanSend(DataServerConsole console) => false;

        public Task<string> SendCommandAsync(DataServerConsole console, string command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task ServerStatusChangesArriveInOrder()
    {
        using var changed = Record<ServerStatusChangedEvent>();

        var engine = new StatusEngine();
        var manager = new ServerManager(
            NullLogger<ServerManager>.Instance,
            ApplicationFixture.Instance.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            [engine],
            GetService<SettingsProvider<ServerSettings>>(),
            new NoRcon(),
            pluginEventBus: GetService<IPluginEventBus>());

        await manager.InitializeAsync();

        var server = new ServerModel { Id = Guid.NewGuid(), Name = Unique("Server") };

        engine.Raise(new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Starting));
        engine.Raise(new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Retrieving));
        engine.Raise(new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Running));
        engine.Raise(new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error, new InvalidOperationException("crashed")));

        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (changed.Events.Count(e => e.ServerId == server.Id) < 3 && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        var events = changed.Events.Where(e => e.ServerId == server.Id).ToList();

        events.Select(e => e.Status).ShouldBe([ServerEngineStatus.Starting, ServerEngineStatus.Running, ServerEngineStatus.Error]);
        events.ShouldAllBe(e => e.ServerName == server.Name);
        events[^1].Error.ShouldBe("crashed");
    }
}
