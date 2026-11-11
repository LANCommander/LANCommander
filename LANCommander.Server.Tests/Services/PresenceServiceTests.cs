using System.Collections.Concurrent;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using PresenceActivity = LANCommander.SDK.Models.PresenceActivity;
using UserPresence = LANCommander.SDK.Models.UserPresence;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// <see cref="PresenceService"/> over the application's database, cache and event bus, with
/// what it would send to launchers recorded rather than sent.
/// </summary>
[Collection("Application")]
public class PresenceServiceTests(ApplicationFixture fixture) : DalTest(fixture), IAsyncLifetime
{
    private sealed record Sent(string Kind, Guid UserId, Guid? RecipientId, UserPresence? Presence);

    private sealed class RecordingBroadcaster : IPresenceBroadcaster
    {
        public ConcurrentQueue<Sent> Sent { get; } = new();

        public Task ChangedAsync(UserPresence presence)
        {
            Sent.Enqueue(new Sent("Changed", presence.UserId, null, presence));
            return Task.CompletedTask;
        }

        public Task ChangedAsync(Guid recipientId, UserPresence presence)
        {
            Sent.Enqueue(new Sent("Changed", presence.UserId, recipientId, presence));
            return Task.CompletedTask;
        }

        public Task EndedAsync(Guid userId)
        {
            Sent.Enqueue(new Sent("Ended", userId, null, null));
            return Task.CompletedTask;
        }

        public Sent[] For(Guid userId) => Sent.Where(s => s.UserId == userId).ToArray();
    }

    private readonly RecordingBroadcaster _broadcaster = new();
    private PresenceService _presence = null!;

    public async Task InitializeAsync()
    {
        _presence = new PresenceService(
            GetService<IFusionCache>(),
            GetService<IDbContextFactory<DatabaseContext>>(),
            GetService<IServiceScopeFactory>(),
            _broadcaster,
            GetService<IPluginEventBus>(),
            NullLogger<PresenceService>.Instance);

        await _presence.StartAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => _presence.StopAsync(CancellationToken.None);

    private Task<User> AddUserAsync(string? alias = null) =>
        GetService<UserService>().AddAsync(new User { UserName = Unique("player"), Alias = alias, Approved = true });

    private async Task<(User User, Game Game)> StartPlayingAsync(string? alias = null)
    {
        var user = await AddUserAsync(alias);
        var game = await AddGameAsync();

        await GetService<PlaySessionService>().StartSessionAsync(game.Id, user.Id);

        return (user, game);
    }

    private static PresenceActivity Activity(string details = "de_dust2") => new()
    {
        Details = details,
        State = "Score: 7 - 3",
        PartySize = 3,
        PartyMax = 8,
    };

    [Fact]
    public async Task PluginsAreToldWhatEveryoneMaySee()
    {
        var changes = new ConcurrentQueue<LANCommander.Server.Plugins.PresenceChangedEvent>();
        using var subscription = GetService<IPluginEventBus>().Subscribe<LANCommander.Server.Plugins.PresenceChangedEvent>((e, _) =>
        {
            changes.Enqueue(e);
            return Task.CompletedTask;
        });

        var (user, game) = await StartPlayingAsync();

        changes.ShouldContain(e => e.UserId == user.Id && e.Presence != null && e.Presence.GameId == game.Id);

        await _presence.UpdateActivityAsync(user.Id, game.Id, Activity());

        changes.Last(e => e.UserId == user.Id).Presence!.Activity!.Details.ShouldBe("de_dust2");

        // The player still sees their own activity, but nobody else does, plugins included
        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.GameOnly);

        var gameOnly = changes.Last(e => e.UserId == user.Id).Presence!;
        gameOnly.GameId.ShouldBe(game.Id);
        gameOnly.Activity.ShouldBeNull();

        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.Hidden);

        changes.Last(e => e.UserId == user.Id).Presence.ShouldBeNull();

        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.Visible);
        await GetService<PlaySessionService>().EndSessionAsync(game.Id, user.Id);

        changes.Last(e => e.UserId == user.Id).Presence.ShouldBeNull();
    }

    [Fact]
    public async Task StartingAGameTellsEveryoneWhoIsPlayingIt()
    {
        var user = await AddUserAsync(alias: "Ace");
        var game = await AddGameAsync();

        var icon = await AddMediaAsync(game.Id);
        icon.Type = MediaType.Icon;
        await GetService<MediaService>().UpdateAsync(icon);

        await GetService<PlaySessionService>().StartSessionAsync(game.Id, user.Id);

        var sent = _broadcaster.For(user.Id).ShouldHaveSingleItem();
        sent.Kind.ShouldBe("Changed");
        sent.RecipientId.ShouldBeNull();
        sent.Presence!.UserName.ShouldBe("Ace");
        sent.Presence.GameId.ShouldBe(game.Id);
        sent.Presence.GameTitle.ShouldBe(game.Title);
        sent.Presence.IconMediaId.ShouldBe(icon.Id);
        sent.Presence.Activity.ShouldBeNull();
    }

    [Fact]
    public async Task EndingAGameTellsEveryoneAndForgetsTheActivity()
    {
        var (user, game) = await StartPlayingAsync();
        await _presence.UpdateActivityAsync(user.Id, game.Id, Activity());

        await GetService<PlaySessionService>().EndSessionAsync(game.Id, user.Id);

        _broadcaster.For(user.Id).Last().Kind.ShouldBe("Ended");
        (await _presence.GetAllAsync(user.Id)).ShouldNotContain(p => p.UserId == user.Id);

        // Playing again starts with no activity left over
        await GetService<PlaySessionService>().StartSessionAsync(game.Id, user.Id);

        (await _presence.GetAllAsync(user.Id)).Single(p => p.UserId == user.Id).Activity.ShouldBeNull();
    }

    [Fact]
    public async Task ActivityIsSharedWithTheGame()
    {
        var (user, game) = await StartPlayingAsync();
        var viewer = await AddUserAsync();

        await _presence.UpdateActivityAsync(user.Id, game.Id, Activity());

        var sent = _broadcaster.For(user.Id).Last();
        sent.Presence!.Activity!.Details.ShouldBe("de_dust2");

        var seen = (await _presence.GetAllAsync(viewer.Id)).Single(p => p.UserId == user.Id);
        seen.Activity!.State.ShouldBe("Score: 7 - 3");
        seen.Activity.PartySize.ShouldBe(3);
        seen.Activity.PartyMax.ShouldBe(8);
    }

    [Fact]
    public async Task ActivityForAGameThePlayerIsNotInIsIgnored()
    {
        var (user, _) = await StartPlayingAsync();
        var otherGame = await AddGameAsync();
        var idle = await AddUserAsync();

        await _presence.UpdateActivityAsync(user.Id, otherGame.Id, Activity());
        await _presence.UpdateActivityAsync(idle.Id, otherGame.Id, Activity());

        _broadcaster.For(user.Id).ShouldHaveSingleItem(); // just the session start
        _broadcaster.For(idle.Id).ShouldBeEmpty();
        (await _presence.GetAllAsync(idle.Id)).ShouldNotContain(p => p.UserId == idle.Id);
    }

    [Fact]
    public async Task GameOnlyHidesTheActivityFromEveryoneButThePlayer()
    {
        var (user, game) = await StartPlayingAsync();
        var viewer = await AddUserAsync();

        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.GameOnly);
        await _presence.UpdateActivityAsync(user.Id, game.Id, Activity());

        var sent = _broadcaster.For(user.Id).TakeLast(2).ToArray();
        sent[0].RecipientId.ShouldBeNull();
        sent[0].Presence!.Activity.ShouldBeNull();
        sent[1].RecipientId.ShouldBe(user.Id);
        sent[1].Presence!.Activity!.Details.ShouldBe("de_dust2");

        (await _presence.GetAllAsync(viewer.Id)).Single(p => p.UserId == user.Id).Activity.ShouldBeNull();
        (await _presence.GetAllAsync(user.Id)).Single(p => p.UserId == user.Id).Activity.ShouldNotBeNull();
    }

    [Fact]
    public async Task HiddenPlayersAreOnlyVisibleToThemselves()
    {
        var (user, _) = await StartPlayingAsync();
        var viewer = await AddUserAsync();

        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.Hidden);

        var sent = _broadcaster.For(user.Id).TakeLast(2).ToArray();
        sent[0].Kind.ShouldBe("Ended");
        sent[1].Kind.ShouldBe("Changed");
        sent[1].RecipientId.ShouldBe(user.Id);

        (await _presence.GetAllAsync(viewer.Id)).ShouldNotContain(p => p.UserId == user.Id);
        (await _presence.GetAllAsync(user.Id)).ShouldContain(p => p.UserId == user.Id);

        // Showing again tells everyone at once
        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.Visible);

        var shown = _broadcaster.For(user.Id).Last();
        shown.Kind.ShouldBe("Changed");
        shown.RecipientId.ShouldBeNull();
    }

    [Fact]
    public async Task VisibilityIsKeptAsAUserCustomField()
    {
        var user = await AddUserAsync();
        var customFields = GetService<UserCustomFieldService>();

        (await _presence.GetVisibilityAsync(user.Id)).ShouldBe(PresenceVisibility.Visible);

        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.GameOnly);

        (await customFields.GetAsync(user.Id, PresenceService.VisibilityFieldName))!.Value.ShouldBe("GameOnly");
        (await _presence.GetVisibilityAsync(user.Id)).ShouldBe(PresenceVisibility.GameOnly);

        // Back to the default leaves no field behind
        await _presence.SetVisibilityAsync(user.Id, PresenceVisibility.Visible);

        (await customFields.GetAsync(user.Id, PresenceService.VisibilityFieldName)).ShouldBeNull();
    }

    [Fact]
    public void ReportedActivityIsSanitized()
    {
        var clean = PresenceService.Sanitize(new PresenceActivity
        {
            Details = "  " + new string('x', 500) + "  ",
            State = "   ",
            PartySize = 12,
            PartyMax = 8,
            LargeImageUrl = "game-logo",                     // a Discord asset key
            SmallImageUrl = "https://example.com/rank.png",
        })!;

        clean.Details!.Length.ShouldBe(PresenceService.MaxTextLength);
        clean.State.ShouldBeNull();
        clean.PartySize.ShouldBe(8);
        clean.LargeImageUrl.ShouldBeNull();
        clean.SmallImageUrl.ShouldBe("https://example.com/rank.png");

        PresenceService.Sanitize(new PresenceActivity { Details = " ", LargeImageUrl = "javascript:alert(1)" }).ShouldBeNull();
    }
}
