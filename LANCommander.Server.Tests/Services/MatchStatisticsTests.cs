using System.Text.Json;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.MatchStatistics;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using User = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Match statistics over the application's database, queue and background writer: events go in
/// through <see cref="MatchService"/> and are read back once the writer has written them.
/// </summary>
[Collection("Application")]
public class MatchStatisticsTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(15);

    private MatchService Matches => GetService<MatchService>();
    private MatchQueryService Queries => GetService<MatchQueryService>();
    private PlayerIdentityService Identities => GetService<PlayerIdentityService>();

    private async Task<User> AddUserAsync(string? alias = null) =>
        await GetService<UserService>().AddAsync(new User { UserName = Unique("player"), Alias = alias, Approved = true });

    private static MatchReporterContext Trusted(User user) => new(user.Id, null, MatchReporterTrust.Authoritative, true);

    private static MatchReporterContext Client(User user) => new(user.Id, null, MatchReporterTrust.Untrusted, false);

    private static MatchEventSubmission Event(long sequence, string type = "kill", string? actor = null, string? subject = null, object? payload = null) => new()
    {
        Id = Guid.NewGuid(),
        Sequence = sequence,
        Type = type,
        GameTimeMilliseconds = sequence * 1000,
        ActorKey = actor,
        SubjectKey = subject,
        Payload = payload == null ? null : JsonSerializer.SerializeToElement(payload),
    };

    private async Task<MatchPage<MatchEventInformation>> WaitForEventsAsync(Guid matchId, int count)
    {
        var deadline = DateTime.UtcNow + WriteTimeout;

        while (true)
        {
            var page = await Queries.GetEventsAsync(matchId, new MatchEventQuery { PageSize = 5000 });

            if (page.Total >= count || DateTime.UtcNow > deadline)
                return page;

            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task CreatingAMatchAgainWithTheSameExternalIdReturnsIt()
    {
        var game = await AddGameAsync();
        var reporter = await AddUserAsync();
        var externalId = Unique("round");

        var first = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id, ExternalId = externalId, Map = "de_dust2", ReporterName = "srv" }, Trusted(reporter));
        var second = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id, ExternalId = externalId, ReporterName = "srv" }, Trusted(reporter));

        first.Created.ShouldBeTrue();
        second.Created.ShouldBeFalse();
        second.MatchId.ShouldBe(first.MatchId);
        second.ReporterId.ShouldBe(first.ReporterId);

        var match = await Queries.GetMatchAsync(first.MatchId);

        match!.Map.ShouldBe("de_dust2");
        match.Reporters.Single().Trust.ShouldBe(MatchReporterTrust.Authoritative);
    }

    [Fact]
    public async Task EventsAreWrittenOnceWithGapsCountedAndPlayersAdded()
    {
        var game = await AddGameAsync();
        var reporter = await AddUserAsync();
        var registration = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id }, Trusted(reporter));

        var kill = Event(1, actor: "steam:1", subject: "steam:2", payload: new { weapon = "rifle" });

        await Matches.RecordAsync(registration.MatchId, registration.ReporterId, [kill, Event(2, actor: "steam:2"), Event(5, "round_ended")], Trusted(reporter), waitForRoom: false);

        // A retry of an event already sent is dropped by its id
        await Matches.RecordAsync(registration.MatchId, registration.ReporterId, [kill], Trusted(reporter), waitForRoom: false);

        var events = await WaitForEventsAsync(registration.MatchId, 3);

        events.Total.ShouldBe(3);
        events.Items[0].Payload!.Value.GetProperty("weapon").GetString().ShouldBe("rifle");

        // Give the retried batch time to be written, if it were going to be
        await Task.Delay(500);
        (await Queries.GetEventsAsync(registration.MatchId, new MatchEventQuery())).Total.ShouldBe(3);

        var match = await Queries.GetMatchAsync(registration.MatchId);
        var reporterState = match!.Reporters.Single();

        reporterState.LastSequence.ShouldBe(5);
        reporterState.GapCount.ShouldBe(2);
        match.Participants.Select(p => p.PlayerKey).OrderBy(k => k).ShouldBe(["steam:1", "steam:2"]);
    }

    [Fact]
    public async Task InvalidEventsAreRejectedAndTheRestQueued()
    {
        var game = await AddGameAsync();
        var reporter = await AddUserAsync();
        var registration = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id }, Trusted(reporter));

        var good = Event(1);
        var noSequence = Event(0);
        var notAnObject = Event(2);
        notAnObject.Payload = JsonSerializer.SerializeToElement(new[] { 1, 2 });
        var repeated = Event(1);

        var result = await Matches.RecordAsync(registration.MatchId, registration.ReporterId, [good, noSequence, notAnObject, repeated], Trusted(reporter), waitForRoom: false);

        result.Queued.ShouldBe(1);
        result.Rejected.Select(r => r.Id).ShouldBe([noSequence.Id, notAnObject.Id, repeated.Id], ignoreOrder: true);
        result.Rejected.Single(r => r.Id == repeated.Id).Code.ShouldBe(MatchEventRejectionCodes.Duplicate);
    }

    [Fact]
    public async Task APlayerCantSendEventsAsSomeoneElsesReporter()
    {
        var game = await AddGameAsync();
        var owner = await AddUserAsync();
        var other = await AddUserAsync();
        var registration = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id, ReporterKind = MatchReporterKind.Client }, Client(owner));

        await Should.ThrowAsync<MatchAccessDeniedException>(() =>
            Matches.RecordAsync(registration.MatchId, registration.ReporterId, [Event(1)], Client(other), waitForRoom: false));
    }

    [Fact]
    public async Task AClientCantEndAMatchATrustedReporterIsRecording()
    {
        var game = await AddGameAsync();
        var server = await AddUserAsync();
        var player = await AddUserAsync();
        var externalId = Unique("match");

        var registration = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id, ExternalId = externalId }, Trusted(server));
        await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id, ExternalId = externalId, ReporterKind = MatchReporterKind.Client }, Client(player));

        await Should.ThrowAsync<MatchAccessDeniedException>(() =>
            Matches.EndAsync(registration.MatchId, new MatchEndRequest { Reason = "completed" }, Client(player)));
    }

    [Fact]
    public async Task EndingAMatchReplacesResultsAndFeedsTheLeaderboard()
    {
        var game = await AddGameAsync();
        var reporter = await AddUserAsync();
        var alice = await AddUserAsync();
        var bob = await AddUserAsync();

        await Identities.AddAsync(new PlayerIdentitySubmission { UserId = alice.Id, PlayerKey = $"steam:{alice.Id:N}" }, PlayerIdentitySource.Manual, null);
        await Identities.AddAsync(new PlayerIdentitySubmission { UserId = bob.Id, PlayerKey = $"steam:{bob.Id:N}" }, PlayerIdentitySource.Manual, null);

        async Task PlayAsync(int aliceKills, int bobKills)
        {
            var registration = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id }, Trusted(reporter));

            // Results sent twice: the second replaces the first
            await Matches.EndAsync(registration.MatchId, new MatchEndRequest
            {
                Results = [new MatchResultSubmission { PlayerKey = $"steam:{bob.Id:N}", Rank = 1 }],
            }, Trusted(reporter));

            await Matches.EndAsync(registration.MatchId, new MatchEndRequest
            {
                Reason = "completed",
                Results =
                [
                    new MatchResultSubmission { PlayerKey = $"steam:{alice.Id:N}", Rank = 1, Score = aliceKills },
                    new MatchResultSubmission { PlayerKey = $"steam:{bob.Id:N}", Rank = 2, Score = bobKills },
                ],
                Statistics =
                [
                    new MatchStatisticSubmission { PlayerKey = $"steam:{alice.Id:N}", Key = "kills", Value = aliceKills },
                    new MatchStatisticSubmission { PlayerKey = $"steam:{bob.Id:N}", Key = "kills", Value = bobKills },
                ],
            }, Trusted(reporter));

            var match = await Queries.GetMatchAsync(registration.MatchId);

            match!.Status.ShouldBe(MatchStatus.Ended);
            match.Results.Count.ShouldBe(2);
            match.Results[0].UserId.ShouldBe(alice.Id);
        }

        await PlayAsync(10, 4);
        await PlayAsync(7, 6);

        var leaderboard = await Queries.AggregateAsync(new MatchStatisticQuery { GameId = game.Id, Key = "kills" });

        leaderboard.Select(r => (r.UserId, r.Value, r.MatchCount)).ShouldBe([(alice.Id, 17d, 2), (bob.Id, 10d, 2)]);

        var best = await Queries.AggregateAsync(new MatchStatisticQuery { GameId = game.Id, Key = "kills", Aggregation = MatchStatisticAggregation.Minimum, Descending = false });

        best.First().Value.ShouldBe(4);
    }

    [Fact]
    public async Task AddingAnIdentityLinksPlayersAlreadyRecordedInAnyCase()
    {
        var game = await AddGameAsync();
        var reporter = await AddUserAsync();
        var player = await AddUserAsync();
        var name = Unique("Sniper");

        var registration = await Matches.CreateAsync(new MatchCreateRequest
        {
            GameId = game.Id,
            Participants = [new MatchParticipantSubmission { PlayerKey = $"name:{name.ToUpperInvariant()}" }],
        }, Trusted(reporter));

        (await Queries.GetMatchAsync(registration.MatchId))!.Participants.Single().UserId.ShouldBeNull();

        await Identities.AddAsync(new PlayerIdentitySubmission { UserId = player.Id, GameId = game.Id, PlayerKey = $"name:{name}" }, PlayerIdentitySource.Manual, null);

        var participant = (await Queries.GetMatchAsync(registration.MatchId))!.Participants.Single();

        participant.UserId.ShouldBe(player.Id);
        participant.LinkSource.ShouldBe(MatchParticipantLinkSource.Identity);
    }

    [Fact]
    public async Task AnIdentityCantBeClaimedByASecondUser()
    {
        var first = await AddUserAsync();
        var second = await AddUserAsync();
        var key = $"steam:{Guid.NewGuid():N}";

        await Identities.AddAsync(new PlayerIdentitySubmission { UserId = first.Id, PlayerKey = key }, PlayerIdentitySource.Manual, null);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            Identities.AddAsync(new PlayerIdentitySubmission { UserId = second.Id, PlayerKey = key }, PlayerIdentitySource.Manual, null));
    }

    [Fact]
    public async Task APlayerOnTheMatchsServerUnderTheNameIsLinkedAndRemembered()
    {
        var game = await AddGameAsync();
        var server = await AddServerAsync();
        var otherServer = await AddServerAsync();
        var reporter = await AddUserAsync();
        var onServer = await AddUserAsync();
        var elsewhere = await AddUserAsync();
        var alias = Unique("Ace");

        var playSessions = GetService<PlaySessionService>();

        // Two players with the same name, on different servers
        await playSessions.StartSessionAsync(game.Id, onServer.Id, server.Id, alias);
        await playSessions.StartSessionAsync(game.Id, elsewhere.Id, otherServer.Id, alias);

        var registration = await Matches.CreateAsync(new MatchCreateRequest
        {
            GameId = game.Id,
            ServerId = server.Id,
            Participants = [new MatchParticipantSubmission { PlayerKey = "steam:STEAM_0:1:" + Guid.NewGuid().ToString("N")[..8], DisplayName = alias }],
        }, Trusted(reporter));

        var participant = (await Queries.GetMatchAsync(registration.MatchId))!.Participants.Single();

        participant.UserId.ShouldBe(onServer.Id);
        participant.LinkSource.ShouldBe(MatchParticipantLinkSource.Session);

        var remembered = await Identities.GetForUserAsync(onServer.Id);

        remembered.ShouldContain(i => i.PlayerKey == $"name:{alias.ToLowerInvariant()}" && i.GameId == game.Id && i.Source == PlayerIdentitySource.Inferred);
    }

    [Fact]
    public async Task OpenMatchesThatStopReportingAreAbandoned()
    {
        var game = await AddGameAsync();
        var reporter = await AddUserAsync();
        var registration = await Matches.CreateAsync(new MatchCreateRequest { GameId = game.Id }, Trusted(reporter));

        await using (var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync())
        {
            var match = await context.Matches!.SingleAsync(m => m.Id == registration.MatchId);
            match.CreatedOn = DateTime.UtcNow.AddHours(-2);
            await context.SaveChangesAsync();
        }

        await Matches.AbandonStaleAsync(TimeSpan.FromMinutes(30));

        (await Queries.GetMatchAsync(registration.MatchId))!.Status.ShouldBe(MatchStatus.Abandoned);
    }

    [Fact]
    public void TheQueueTakesABatchWholeOrNotAtAll()
    {
        var queue = new ChannelMatchEventQueue(() => 3);

        QueuedMatchEvent Queued() => new(Guid.NewGuid(), Guid.Empty, Guid.Empty, Guid.Empty, 1, "kill", 0, null, null, null, null, null, DateTime.UtcNow);

        queue.TryEnqueue([Queued(), Queued()]).ShouldBeTrue();
        queue.TryEnqueue([Queued(), Queued()]).ShouldBeFalse();
        queue.Count.ShouldBe(2);

        queue.TakeAll().Count.ShouldBe(2);
        queue.TryEnqueue([Queued(), Queued(), Queued()]).ShouldBeTrue();
    }

    [Fact]
    public void SessionsOnAnotherServerNeverLinkAndTwoUsersMeanNoLink()
    {
        var server = Guid.NewGuid();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        PlayerIdentityService.PickSessionUser([(alice, server, "Ace"), (bob, Guid.NewGuid(), "Ace")], server, "ace").ShouldBe(alice);
        PlayerIdentityService.PickSessionUser([(alice, null, "Ace"), (bob, Guid.NewGuid(), "Ace")], server, "Ace").ShouldBe(alice);
        PlayerIdentityService.PickSessionUser([(alice, null, "Ace"), (bob, null, "Ace")], null, "Ace").ShouldBeNull();
        PlayerIdentityService.PickSessionUser([(alice, null, "Ace")], null, "Someone").ShouldBeNull();
    }

    [Theory]
    [InlineData("matches:read", "GET", "/api/Matches", true)]
    [InlineData("matches:read", "POST", "/api/Matches", false)]
    [InlineData("matches:read", null, "/api/Matches", false)]
    [InlineData("matches:report", "POST", "/api/Matches/x/Events", true)]
    [InlineData("matches:report", "GET", "/api/Games", false)]
    [InlineData("matches:report", "GET", "/api/MatchesAdmin", false)]
    public void MatchScopesOnlyReachTheMatchEndpoints(string scope, string? method, string path, bool allowed) =>
        PersonalAccessTokenScopes.Parse(scope).Allows(new PathString(path), method).ShouldBe(allowed);
}
