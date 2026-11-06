using System.Collections.Concurrent;
using LANCommander.Interposer.Events;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class PresenceForwarderTests
{
    [Fact]
    public void OnlyWhatCanBeShownOutsideDiscordIsForwarded()
    {
        var activity = PresenceForwarder.ToActivity(new RichPresence
        {
            Details = "de_dust2",
            State = " Score: 7 - 3 ",
            TimestampStart = 1_700_000_000,
            PartySize = 3,
            PartyMax = 8,
            LargeImage = "game-logo",                      // a Discord asset key
            LargeImageText = "Counter-Strike",
            SmallImage = "https://example.com/rank.png",
        }, "Counter-Strike")!;

        activity.Details.ShouldBe("de_dust2");
        activity.State.ShouldBe("Score: 7 - 3");
        activity.StartsOn.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        activity.EndsOn.ShouldBeNull();
        activity.PartySize.ShouldBe(3);
        activity.PartyMax.ShouldBe(8);
        activity.LargeImageUrl.ShouldBeNull();
        activity.SmallImageUrl.ShouldBe("https://example.com/rank.png");
    }

    [Fact]
    public void TheGamesTitleAloneIsNoActivity()
    {
        // The launcher's own default for RichPresence.Details
        PresenceForwarder.ToActivity(new RichPresence { Details = "Quake III Arena", LargeImage = "logo" }, "quake iii arena")
            .ShouldBeNull();

        PresenceForwarder.ToActivity(null, "Quake III Arena").ShouldBeNull();
    }

    private sealed class Recorder
    {
        public ConcurrentQueue<string?> Sent { get; } = new();

        public PresenceForwarder.Tracker Track(TimeSpan interval) => new(
            Guid.NewGuid(), "Game", null,
            (activity, _) =>
            {
                Sent.Enqueue(activity?.Details);
                return Task.CompletedTask;
            },
            interval, NullLogger.Instance);
    }

    private static PresenceActivity Doing(string details) => new() { Details = details };

    [Fact]
    public async Task ReportsInQuickSuccessionSendOnlyTheLatest()
    {
        var recorder = new Recorder();
        using var tracker = recorder.Track(TimeSpan.FromMilliseconds(300));

        tracker.Report(Doing("first"));
        tracker.Report(Doing("second"));
        tracker.Report(Doing("third"));

        await Task.Delay(800);

        recorder.Sent.ShouldBe(["first", "third"]);
    }

    [Fact]
    public async Task AClearIsForwardedAsNull()
    {
        var recorder = new Recorder();
        using var tracker = recorder.Track(TimeSpan.FromMilliseconds(50));

        tracker.Report(Doing("in a match"));
        await Task.Delay(150);
        tracker.Report(null);
        await Task.Delay(150);

        recorder.Sent.ShouldBe(["in a match", null]);
    }

    [Fact]
    public async Task NothingIsSentOnceTheGameHasExited()
    {
        var recorder = new Recorder();
        var tracker = recorder.Track(TimeSpan.FromMilliseconds(300));

        tracker.Report(Doing("first"));
        tracker.Report(Doing("pending"));
        tracker.Dispose();
        tracker.Report(Doing("after exit"));

        await Task.Delay(600);

        recorder.Sent.ShouldBe(["first"]);
    }
}
